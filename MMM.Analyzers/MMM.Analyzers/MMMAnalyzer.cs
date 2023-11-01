

namespace MMM.Analyzers
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using MMM.Analyzers.Helpers;
    using System;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis.Scripting;
    using Microsoft.CodeAnalysis.CSharp.Scripting;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MMMAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MMMAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "MMM";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.Attribute);
        }


        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var attribute = (AttributeSyntax)context.Node;
            var semanticModel = context.SemanticModel;
            var attributeType = semanticModel.GetTypeInfo(attribute).Type;

            if (IsAttributeOfType(attributeType, typeof(Attributes.PublishesAttribute)))
            {
                var args = GetAttributeArguments(context, attribute, typeof(Attributes.PublishesAttribute));
                SendAttributeInfo(context, Enums.MessageDirection.Publish, args).GetAwaiter().GetResult();
            }
            else if (IsAttributeOfType(attributeType, typeof(Attributes.SubscribesAttribute)))
            {
                var args = GetAttributeArguments(context, attribute, typeof(Attributes.SubscribesAttribute));
                SendAttributeInfo(context, Enums.MessageDirection.Subscribe, args).GetAwaiter().GetResult();
            }
        }

        private static bool IsAttributeOfType(ITypeSymbol attributeType, Type targetType)
        {
            return (attributeType as INamedTypeSymbol).ConstructedFrom.ToDisplayString() == targetType.FullName;
        }

        private static Dictionary<string, object> GetAttributeArguments(SyntaxNodeAnalysisContext context, AttributeSyntax attribute, Type attributeType)
        {
            var passedArgs = attribute.ArgumentList.Arguments.Select(argumentSyntax => EvaluateExpression(context, argumentSyntax.Expression)).ToArray();
            var originalArgs = attributeType.GetConstructors().FirstOrDefault()?.GetParameters().Select(param => param.Name).ToArray();
            if (originalArgs == null)
            {
                throw new InvalidOperationException($"The attribute {attributeType.Name} does not have a constructor.");
            }
            return Enumerable.Range(0, originalArgs.Length).ToDictionary(i => originalArgs[i], i => i < passedArgs.Length ? passedArgs[i] : null);
        }

        private static async Task SendAttributeInfo(SyntaxNodeAnalysisContext context, Enums.MessageDirection direction, Dictionary<string, object> args)
        {
            string messageName = Convert.ToString(args["messageName"]);
            string messageSchema = new JsonSchemaGenerator().GenerateJsonSchema(args["messageType"] as ITypeSymbol);
            string moduleName = Convert.ToString(args["moduleName"]);
            string messageDescription = Convert.ToString(args["messageDescription"]);
            string assemblyName = context.Compilation.AssemblyName;
            string compilationId = context.Compilation.SyntaxTrees.Aggregate(0, (i, s) => i + s.Length).ToString();
            string location = context.Node.GetLocation().ToString();
            await HttpClientHelper.Send(direction, messageName, messageSchema, moduleName, messageDescription, assemblyName, compilationId, location);
        }

        private static object EvaluateExpression(SyntaxNodeAnalysisContext context, ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax literalExpression)
            {
                var expressionValue = context.SemanticModel.GetConstantValue(literalExpression);
                if (expressionValue.HasValue)
                {
                    return expressionValue.Value;
                }
            }
            if (expression is IdentifierNameSyntax IdentifierNameSyntax)
            {
                var expressionValue = context.SemanticModel.GetConstantValue(IdentifierNameSyntax);
                if (expressionValue.HasValue)
                {
                    return expressionValue.Value;
                }
            }
            else if (expression is TypeOfExpressionSyntax typeOfExpression)
            {
                var expressionValue = context.SemanticModel.GetTypeInfo(typeOfExpression.Type);
                if (expressionValue.Type != null)
                {
                    return expressionValue.Type;
                }
            }
            else if (expression is InvocationExpressionSyntax invocationExpressionSyntax)
            {
                if ((invocationExpressionSyntax.Expression as IdentifierNameSyntax)?.Identifier.Text == "nameof")
                    return invocationExpressionSyntax.ArgumentList.Arguments[0].GetText().ToString();

            }
            return null;
        }
    }
}