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
        ImmutableArray.Create(DiagnosticDescriptors.ExtensionRequiresCollection, DiagnosticDescriptors.UseResolveAsyncForAsyncExpression);

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

        if (CollectionExtensions.Contains(invocation.TargetMethod.Name))
            AnalyzeCollectionExtension(context, invocation);
        else if (invocation.TargetMethod.Name == "Resolve")
            AnalyzeSyncResolve(context, invocation);
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
