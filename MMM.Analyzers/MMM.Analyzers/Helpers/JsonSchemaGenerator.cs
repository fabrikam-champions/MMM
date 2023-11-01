using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;

namespace MMM.Analyzers.Helpers
{
    public class JsonSchemaGenerator
    {
        public string GenerateJsonSchema(ITypeSymbol symbol)
        {
            var stringBuilder = new StringBuilder();
            if (IsArray(symbol))
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
                .ToList();
            if (type.BaseType != null)
                propertySymbols.AddRange(GetMembers(type.BaseType));
            return propertySymbols;

        }
        private void GenerateClassSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append("{");
            stringBuilder.AppendLine();
            var properties = GetMembers(type);

            for (int i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var propertyType = property.Type;
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
            if (IsArray(type))
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

        private void GenerateEnumSchema(ITypeSymbol type, StringBuilder stringBuilder, int level)
        {
            stringBuilder.Append('[');
            var members = type.GetMembers();
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
            stringBuilder.Append(']');
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
            stringBuilder.Append($"\"{type.ToDisplayString()}\"");
        }
        private bool IsEnum(ITypeSymbol type)
        {
            return type.TypeKind == TypeKind.Enum;
        }

        private bool IsArray(ITypeSymbol type)
        {
            return type.TypeKind == TypeKind.Array || ((INamedTypeSymbol)type).IsGenericType && new[] { "List", "Array", "Enumerable", "Collection" }.Any(name => type.Name.Contains(name));
        }
        private bool IsClass(ITypeSymbol type)
        {
            return type.TypeKind == TypeKind.Class && !new[] { "string", "string?" }.Contains(type.ToDisplayString());
        }
    }
}
