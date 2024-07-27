using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MMMScanner
{
    internal class JsonSchemaGenerator
    {
        public string GenerateJsonSchema(ITypeSymbol symbol)
        {
            var stringBuilder = new StringBuilder();
            if (IsDynamic(symbol))
            {
                GenerateDynamicSchema(symbol, stringBuilder, 0);
            }
            else if (IsArray(symbol))
            {
                GenerateArraySchema(symbol, stringBuilder, -1);
            }
            else
            {
                GenerateClassSchema(symbol, stringBuilder, 0);
            }
            return stringBuilder.ToString();
        }
        private IList<IPropertySymbol> GetMembers(ITypeSymbol type)
        {
            var propertySymbols = type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p => p.CanBeReferencedByName && p.GetMethod != null && !p.GetMethod.IsStatic)
                .OrderBy(p => p.Name)
                .ToList();
            if (type.BaseType != null)
                propertySymbols.AddRange(GetMembers(type.BaseType));
            return propertySymbols;

        }
        private string GetSummary(ISymbol type)
        {
            var xml = type.GetDocumentationCommentXml();
            if(string.IsNullOrEmpty(xml))
            {
                return null;
            }
            return System.Xml.Linq.XDocument.Parse(xml).Descendants("summary")?.FirstOrDefault()?.Value.Trim();
        }
        private void GenerateClassSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append("{");
            string summary = GetSummary(type);
            if(!string.IsNullOrEmpty( summary))
            {
                stringBuilder.Append($"/*{summary}*/");
            }
            stringBuilder.AppendLine();
            var properties = GetMembers(type);

            for (int i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var propertyType = property.Type;
                var propertSummary = GetSummary(property);
                if (!string.IsNullOrEmpty(propertSummary) && propertSummary.Replace(" ","").ToLower()!= property.Name.Replace("_","").ToLower())
                {
                    stringBuilder.Append(new string(' ', (level + 1) * 8));
                    stringBuilder.Append($"/*{propertSummary}*/");
                    stringBuilder.AppendLine();
                }
                
                stringBuilder.Append(new string(' ', (level + 1) * 8));
                stringBuilder.Append($"\"{property.Name}\": ");

                GeneratePropertySchema(propertyType, stringBuilder, level);

                if (i < properties.Count - 1)
                {
                    stringBuilder.Append(",");
                }
                stringBuilder.AppendLine();
            }
            stringBuilder.Append(new string(' ', Math.Max(level, 0) * 8));
            stringBuilder.Append("}");
        }

        private void GeneratePropertySchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            if (IsDynamic(type))
            {
                GenerateDynamicSchema(type, stringBuilder, level);
            }
            else if (IsArray(type))
            {
                GenerateArraySchema(type, stringBuilder, level);
            }
            else if (IsEnum(type))
            {
                GenerateEnumSchema(type, stringBuilder, level);
            }
            else if (IsClass(type))
            {
                GenerateClassSchema(type, stringBuilder, level + 1);
            }
            else
            {
                GeneratePrimitiveSchema(type, stringBuilder, level);
            }
        }

        private bool IsDynamic(ITypeSymbol type)
        {
            if (type == null)
                return false;
            return type.TypeKind == TypeKind.Dynamic;
        }

        private void GenerateDynamicSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append("dynamic");
        }

        private void GenerateEnumSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append($"\"{type?.ToDisplayString()}\"");
            stringBuilder.Append("/*[");
            var members = type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? (type as INamedTypeSymbol).TypeArguments.FirstOrDefault().GetMembers() : type.GetMembers();
            for (int i = 0; i < members.Length; i++)
            {
                var member = members[i];
                if (member.Kind == SymbolKind.Field && member is IFieldSymbol fieldSymbol && fieldSymbol.HasConstantValue)
                {
                    stringBuilder.Append($"{fieldSymbol.ConstantValue}-{fieldSymbol.Name}");
                    if (i < members.Length - 2)
                        stringBuilder.Append(", ");
                }
            }
            stringBuilder.Append("]*/");
        }

        private void GenerateArraySchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append("[");

            var elementType = type.TypeKind == TypeKind.Array ? ((IArrayTypeSymbol)type).ElementType : ((INamedTypeSymbol)type).TypeArguments.FirstOrDefault() as ITypeSymbol;
            GeneratePropertySchema(elementType, stringBuilder, level);

            stringBuilder.Append("]");
        }

        private void GeneratePrimitiveSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append($"\"{type?.ToDisplayString()}\"");
        }
        private bool IsEnum(ITypeSymbol type)
        {
            if (type == null)
                return false;
            return type.TypeKind == TypeKind.Enum || 
                (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && (type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault()?.TypeKind == TypeKind.Enum);
        }

        private bool IsArray(ITypeSymbol type)
        {
            if (type == null)
                return false;
            return type.TypeKind == TypeKind.Array || ((INamedTypeSymbol)type).IsGenericType && new[] { "List", "Array", "Enumerable", "Collection" }.Any(name => type.Name.Contains(name));
        }
        private bool IsClass(ITypeSymbol type)
        {
            if (type == null)
                return false;
            return type.TypeKind == TypeKind.Class && !new[] { "string", "string?" }.Contains(type.ToDisplayString());
        }
    }
}
