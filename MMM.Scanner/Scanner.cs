using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MMMScanner
{
    public class Scanner
    {
        public static async Task ScanAsync(Options args)
        {
            try
            {
                using var workspace = MSBuildWorkspace.Create();
                var solution = workspace.OpenSolutionAsync(args.Source).GetAwaiter().GetResult();
                var messagesByMMMAttribute = GetMessagesByMMMAttribute(solution);
                var messagesByCAP = GetMessagesByCAP(solution);
                var solutionPath = Path.GetDirectoryName(solution.FilePath);
                var messages = messagesByMMMAttribute.Union(messagesByCAP)
                    .Where(m => !string.IsNullOrEmpty(m?.Name))
                    .GroupBy(m => new { m.Name, m.Assembly, m.Direction, LocationFilePath = m.Location.Split(':').FirstOrDefault() })
                    .Select(g => new Message { 
                        Name = g.Key.Name, 
                        Module = g.Select(m => m.Module).OrderByDescending(s => s?.Length).FirstOrDefault(), 
                        Assembly = g.Key.Assembly, 
                        Direction = g.Key.Direction, 
                        Location = string.Join("\r\n", g.Select(m => Path.GetRelativePath(solutionPath, m.Location))), 
                        Description = g.Select(m => m.Description).OrderByDescending(s => s?.Length).FirstOrDefault(), 
                        Schema = g.Select(m => m.Schema).OrderByDescending(s=>s?.Length).FirstOrDefault() 
                    }).OrderBy(m => m.Name).ToList();
                var options = new JsonSerializerOptions { WriteIndented = false };
                byte[] json = JsonSerializer.SerializeToUtf8Bytes(messages, options);
                await File.WriteAllBytesAsync(args.Destination, json);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Console.Error.WriteLine($"Stack Trace: {ex.StackTrace}");
                throw;
            }
        }
        private static bool IsCSharpDocument(Document document)
        {
            return Path.GetFileName(document.FilePath).EndsWith(".cs");
        }
        private static IEnumerable<Message> GetMessagesByMMMAttribute(Solution solution)
        {
            return solution.Projects
                .SelectMany(project =>
                    project.Documents
                        .Where(IsCSharpDocument)
                        .SelectMany(GetAttributeSyntaxes)
                        .Where(IsMessageAttribute)
                        .Select(GetMessageFromMMMAttribute));
        }
        private static Message GetMessageFromMMMAttribute((AttributeSyntax attribute, SemanticModel semanticModel) input)
        {
            var attribute = input.attribute;
            var semanticModel = input.semanticModel;
            var attributeType = GetMessageAttributeExactType(semanticModel.GetTypeInfo(attribute).Type);
            var args = GetAttributeArguments(attribute, semanticModel, attributeType);
            string messageName = Convert.ToString(args["messageName"]);
            if(messageName.Contains("INUPCO_CommitteeManagement_CommitteeCreated"))
            {

            }
            string messageSchema = new JsonSchemaGenerator().GenerateJsonSchema(args["messageType"] as ITypeSymbol);
            string moduleName = Convert.ToString(args["moduleName"]);
            string messageDescription = Convert.ToString(args["messageDescription"]);
            string assemblyName = semanticModel.Compilation.AssemblyName;
            string compilationId = semanticModel.Compilation.SyntaxTrees.Aggregate(0, (i, s) => i + s.Length).ToString();
            string location = $"{attribute.GetLocation().GetLineSpan().Path}:{attribute.GetLocation().GetLineSpan().StartLinePosition.Line}";
            string direction = attributeType.Name == nameof(MMM.Attributes.PublishesAttribute) ? Direction.Publish : Direction.Subscribe;
            return new Message { Name = messageName, Schema = messageSchema, Description = messageDescription, Module = moduleName, Assembly = assemblyName, Location = location, Direction = direction };
        }
        private static Message GetMessageFromCapPublishInvocationExpression((InvocationExpressionSyntax invocationExpressionSyntax, SemanticModel semanticModel) input)
        {
            var invocationExpressionSyntax = input.invocationExpressionSyntax;
            var semanticModel = input.semanticModel;
            var args = GetMethodArguments(invocationExpressionSyntax, semanticModel);
            if (args.TryGetValue("name", out var messageName))
            {
                int indexOfContentObjArgument = Array.IndexOf((semanticModel.GetSymbolInfo(invocationExpressionSyntax).Symbol.OriginalDefinition as IMethodSymbol).Parameters.Select(p => p.Name).ToArray(), "contentObj");
                string messageSchema = semanticModel.GetTypeInfo(invocationExpressionSyntax.ArgumentList.Arguments[indexOfContentObjArgument].Expression).Type is ITypeSymbol typeSymbol ? new JsonSchemaGenerator().GenerateJsonSchema(typeSymbol) : null;
                string assemblyName = semanticModel.Compilation.AssemblyName;
                string location = $"{invocationExpressionSyntax.GetLocation().GetLineSpan().Path}:{invocationExpressionSyntax.GetLocation().GetLineSpan().StartLinePosition.Line}";

                return new Message { Name = $"{messageName}", Schema = messageSchema, Assembly = assemblyName, Location = location, Direction = Direction.Publish };
            }
            else return null;
        }
        private static Message GetMessageFromCapSubscribeAttribute((AttributeSyntax attribute, SemanticModel semanticModel) input)
        {
            var attribute = input.attribute;
            var semanticModel = input.semanticModel;
            var args = GetAttributeArguments(attribute, semanticModel, typeof(DotNetCore.CAP.CapSubscribeAttribute));
            string messageName = Convert.ToString(args["name"]);
            string messageSchema = (attribute.Parent.Parent is MethodDeclarationSyntax methodDeclarationSyntax && methodDeclarationSyntax.ParameterList.Parameters.Any()) ? new JsonSchemaGenerator().GenerateJsonSchema(semanticModel.GetTypeInfo(methodDeclarationSyntax.ParameterList.Parameters.First().Type).Type) : null;
            string assemblyName = semanticModel.Compilation.AssemblyName;
            string location = $"{attribute.GetLocation().GetLineSpan().Path}:{attribute.GetLocation().GetLineSpan().StartLinePosition.Line}";
            return new Message { Name = messageName, Schema = messageSchema, Assembly = assemblyName, Location = location, Direction = Direction.Subscribe };
        }
        private static IEnumerable<Message> GetMessagesByCAP(Solution solution)
        {
            var messagesFromCapSubscribeAttribute = solution.Projects
                .SelectMany(project =>
                    project.Documents
                        .Where(IsCSharpDocument)
                        .SelectMany(GetAttributeSyntaxes)
                        .Where(IsCapSubscribeAttribute)
                        .Select(GetMessageFromCapSubscribeAttribute));
            var messagesFromCapPublish = solution.Projects
                .SelectMany(project =>
                    project.Documents
                        .Where(IsCSharpDocument)
                        .SelectMany(InvocationExpressionSyntaxes)
                        .Where(IsCapPublishExpression)
                        .Select(GetMessageFromCapPublishInvocationExpression));
            var messagesByCap = messagesFromCapSubscribeAttribute.Union(messagesFromCapPublish);
            return messagesFromCapPublish;
        }
        private static Type GetMessageAttributeExactType(ITypeSymbol attributeType)
        {
            if ((attributeType as INamedTypeSymbol)?.ConstructedFrom.ToDisplayString() == typeof(MMM.Attributes.PublishesAttribute).FullName)
                return typeof(MMM.Attributes.PublishesAttribute);
            if ((attributeType as INamedTypeSymbol)?.ConstructedFrom.ToDisplayString() == typeof(MMM.Attributes.SubscribesAttribute).FullName)
                return typeof(MMM.Attributes.SubscribesAttribute);
            return null;
        }
        private static Dictionary<string, object> GetAttributeArguments(AttributeSyntax attribute, SemanticModel semanticModel, Type attributeType)
        {
            var passedArgs = attribute.ArgumentList.Arguments.Select(argumentSyntax => EvaluateExpression(semanticModel, argumentSyntax.Expression)).ToArray();
            var originalArgs = attributeType.GetConstructors().FirstOrDefault()?.GetParameters().Select(param => param.Name).ToArray();
            if (originalArgs == null)
            {
                throw new InvalidOperationException($"The attribute {attributeType.Name} does not have a constructor.");
            }
            return Enumerable.Range(0, originalArgs.Length).ToDictionary(i => originalArgs[i], i => i < passedArgs.Length ? passedArgs[i] : null);
        }
        private static Dictionary<string, object> GetMethodArguments(InvocationExpressionSyntax invocationExpressionSyntax, SemanticModel semanticModel)
        {
            var passedArgs = invocationExpressionSyntax.ArgumentList.Arguments.Select(argumentSyntax => EvaluateExpression(semanticModel, argumentSyntax.Expression)).ToArray();
            var originalArgs = (semanticModel.GetSymbolInfo(invocationExpressionSyntax).Symbol.OriginalDefinition as IMethodSymbol).Parameters.Select(param => param.Name).ToArray();
            return Enumerable.Range(0, originalArgs.Length).ToDictionary(i => originalArgs[i], i => i < passedArgs.Length ? passedArgs[i] : null);
        }
        private static IEnumerable<(AttributeSyntax AttributeSyntax, SemanticModel SemanticModel)> GetAttributeSyntaxes(Document document)
        {
            return document.GetSyntaxRootAsync().GetAwaiter().GetResult().DescendantNodesAndSelf().OfType<AttributeSyntax>().Select(attribute => (AttributeSyntax: attribute, SemanticModel: document.GetSemanticModelAsync().GetAwaiter().GetResult()));
        }
        private static IEnumerable<(InvocationExpressionSyntax InvocationExpressionSyntax, SemanticModel SemanticModel)> InvocationExpressionSyntaxes(Document document)
        {
            return document.GetSyntaxRootAsync().GetAwaiter().GetResult().DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Select(expression => (InvocationExpressionSyntax: expression, SemanticModel: document.GetSemanticModelAsync().GetAwaiter().GetResult()));
        }
        private static bool IsMessageAttribute((AttributeSyntax attribute, SemanticModel semanticModel) input)
        {
            var attribute = input.attribute;
            var semanticModel = input.semanticModel;
            var attributeType = semanticModel.GetTypeInfo(attribute).Type;
            return (attributeType as INamedTypeSymbol)?.BaseType?.ToDisplayString() == typeof(MMM.Attributes.MessageAttribute).FullName;
        }
        private static bool IsCapSubscribeAttribute((AttributeSyntax attribute, SemanticModel semanticModel) input)
        {
            var attribute = input.attribute;
            var semanticModel = input.semanticModel;
            var attributeType = semanticModel.GetTypeInfo(attribute).Type;
            return (attributeType as INamedTypeSymbol)?.BaseType?.ToDisplayString() == typeof(DotNetCore.CAP.CapSubscribeAttribute).FullName;
        }
        private static bool IsCapPublishExpression((InvocationExpressionSyntax invocationExpressionSyntax, SemanticModel semanticModel) input)
        {
            var invocationExpressionSyntax = input.invocationExpressionSyntax;
            var semanticModel = input.semanticModel;
            var simpleMemberAccessExpression = invocationExpressionSyntax.DescendantNodes().OfType<MemberAccessExpressionSyntax>().FirstOrDefault(expression => expression.Kind() == SyntaxKind.SimpleMemberAccessExpression);
            if (simpleMemberAccessExpression == null)
                return false;
            return simpleMemberAccessExpression.Name.Identifier.Text.Contains("Publish", StringComparison.InvariantCultureIgnoreCase) &&
                semanticModel.GetTypeInfo(simpleMemberAccessExpression.Expression).Type.ToDisplayString().Contains("Publish", StringComparison.InvariantCultureIgnoreCase);
        }
        private static object EvaluateExpression(SemanticModel semanticModel, ExpressionSyntax expression)
        {
            if (expression is LiteralExpressionSyntax literalExpression)
            {
                var expressionValue = semanticModel.GetConstantValue(literalExpression);
                if (expressionValue.HasValue)
                {
                    return expressionValue.Value;
                }
            }
            if (expression is IdentifierNameSyntax IdentifierNameSyntax)
            {
                var expressionValue = semanticModel.GetConstantValue(IdentifierNameSyntax);
                if (expressionValue.HasValue)
                {
                    return expressionValue.Value;
                }
            }
            else if (expression is TypeOfExpressionSyntax typeOfExpression)
            {
                var expressionValue = semanticModel.GetTypeInfo(typeOfExpression.Type);
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
        private class Message
        {
            public string Name { get; set; }
            public string Schema { get; set; }
            public string Description { get; set; }
            public string Module { get; set; }
            public string Assembly { get; set; }
            public string Location { get; set; }
            public string Direction { get; set; }
        }
        private static class Direction
        {
            public const string Publish = nameof(Publish);
            public const string Subscribe = nameof(Subscribe);
        }
    }
}