using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// Analyzes how a field is defined:
/// EGQL003 - a field extension that needs a collection applied to a field that returns something else.
/// EGQL004 - a synchronous Resolve() whose expression returns a Task.
/// Both mirror exceptions EntityGraphQL throws when the schema is built, reported in the editor instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class FieldDefinitionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Field extensions that build on a collection expression.</summary>
    private static readonly ImmutableHashSet<string> CollectionExtensions = ImmutableHashSet.Create(
        "UseFilter",
        "UseSort",
        "UseOffsetPaging",
        "UseConnectionPaging",
        "UseAggregate"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.ExtensionRequiresCollection,
            DiagnosticDescriptors.UseResolveAsyncForAsyncExpression,
            DiagnosticDescriptors.BlockingAwaitInResolver
        );

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!EntityGraphQLApi.IsEntityGraphQLType(invocation.TargetMethod.ContainingType))
            return;

        var name = invocation.TargetMethod.Name;
        if (CollectionExtensions.Contains(name))
            AnalyzeCollectionExtension(context, invocation);
        else if (name is "Resolve" or "ResolveBulk")
        {
            if (name == "Resolve")
                AnalyzeSyncResolve(context, invocation);
            AnalyzeBlockingAwait(context, invocation);
        }
    }

    /// <summary>EGQL003 - the field the extension is chained onto must produce a collection.</summary>
    private static void AnalyzeCollectionExtension(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var fieldType = FieldReturnTypeFromChain(invocation);
        // unknown shape (e.g. built from a field variable) - say nothing rather than guess
        if (fieldType == null || fieldType.TypeKind == TypeKind.TypeParameter || fieldType.SpecialType == SpecialType.System_Object)
            return;
        if (EntityGraphQLApi.IsCollection(fieldType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.ExtensionRequiresCollection, EntityGraphQLApi.MethodNameLocation(invocation), invocation.TargetMethod.Name, fieldType.ToDisplayString())
        );
    }

    /// <summary>EGQL004 - Resolve() with an expression whose body is a Task needs ResolveAsync().</summary>
    private static void AnalyzeSyncResolve(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var lambdaArgument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "fieldExpression");
        if (lambdaArgument == null)
            return;
        var bodyType = EntityGraphQLApi.LambdaBodyType(lambdaArgument);
        if (bodyType == null || !EntityGraphQLApi.IsAwaitable(bodyType))
            return;

        var fieldName = EntityGraphQLApi.FieldNameFromChain(invocation) ?? "(unnamed)";
        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.UseResolveAsyncForAsyncExpression, EntityGraphQLApi.MethodNameLocation(invocation), fieldName, bodyType.ToDisplayString())
        );
    }

    /// <summary>
    /// EGQL010 - a synchronous resolver that blocks on a Task (.Result, .Wait(), GetAwaiter().GetResult())
    /// rather than using the Async overload. Every lambda argument is checked, since ResolveBulk's blocking
    /// call is in its second one. Reported once per resolver - the first blocking call is the point.
    /// </summary>
    private static void AnalyzeBlockingAwait(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            var lambda = EntityGraphQLApi.Lambda(argument);
            if (lambda == null)
                continue;

            foreach (var operation in lambda.Descendants())
            {
                var blocking = BlockingCall(operation);
                if (blocking == null)
                    continue;

                var fieldName = EntityGraphQLApi.FieldNameFromChain(invocation) ?? "(unnamed)";
                context.ReportDiagnostic(
                    Diagnostic.Create(DiagnosticDescriptors.BlockingAwaitInResolver, operation.Syntax.GetLocation(), fieldName, blocking, invocation.TargetMethod.Name)
                );
                return;
            }
        }
    }

    /// <summary>How a Task is being blocked on, or null if this operation is not blocking on one.</summary>
    private static string? BlockingCall(IOperation operation) =>
        operation switch
        {
            IPropertyReferenceOperation { Property.Name: "Result" } property when EntityGraphQLApi.IsAwaitable(property.Property.ContainingType) => ".Result",
            IInvocationOperation { TargetMethod.Name: "Wait" } wait when EntityGraphQLApi.IsAwaitable(wait.TargetMethod.ContainingType) => ".Wait()",
            // GetResult() lives on TaskAwaiter/ValueTaskAwaiter/ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter
            IInvocationOperation { TargetMethod.Name: "GetResult" } get when get.TargetMethod.ContainingType.Name.EndsWith("Awaiter", System.StringComparison.Ordinal) =>
                "GetAwaiter().GetResult()",
            _ => null,
        };

    /// <summary>
    /// The field's return type, from the AddField/ReplaceField expression overload if the field was declared
    /// with one, otherwise from the lambda body of a Resolve/ResolveAsync later in the chain.
    /// </summary>
    private static ITypeSymbol? FieldReturnTypeFromChain(IInvocationOperation invocation)
    {
        foreach (var link in EntityGraphQLApi.ReceiverChain(invocation))
        {
            if (!EntityGraphQLApi.IsEntityGraphQLType(link.TargetMethod.ContainingType))
                continue;

            if (link.TargetMethod.Name is "AddField" or "ReplaceField")
                return EntityGraphQLApi.FieldExpressionReturnType(link);

            if (link.TargetMethod.Name is "Resolve" or "ResolveAsync")
            {
                var lambdaArgument = link.Arguments.FirstOrDefault(a => a.Parameter?.Name == "fieldExpression");
                if (lambdaArgument != null)
                    return EntityGraphQLApi.LambdaBodyType(lambdaArgument);
            }
        }
        return null;
    }
}
