using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// Analyzes .Resolve&lt;TService&gt;() / .ResolveAsync&lt;TService&gt;() field definitions:
/// EGQL001 - a service field on an entity type with no bulk resolver (N+1 when the type is listed).
/// EGQL002 - an async field using a service that is not safe to use concurrently.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ServiceFieldAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ServiceFieldWithoutBulkResolver, DiagnosticDescriptors.UnsafeServiceInAsyncField);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        var isAsync = method.Name == "ResolveAsync";
        if (!isAsync && method.Name != "Resolve")
            return;
        if (!EntityGraphQLApi.IsEntityGraphQLType(method.ContainingType))
            return;
        // Resolve() with no type arguments takes no service - nothing to batch or run concurrently
        if (method.TypeArguments.Length == 0)
            return;

        var fieldName = EntityGraphQLApi.FieldNameFromChain(invocation) ?? "(unnamed)";
        var location = EntityGraphQLApi.MethodNameLocation(invocation);

        // both sync and async service fields run per item, so both benefit from a bulk resolver
        ReportMissingBulkResolver(context, invocation, method, fieldName, location);
        if (isAsync)
            ReportUnsafeService(context, invocation, method, fieldName, location);
    }

    /// <summary>
    /// EGQL001. Only for fields on entity types - a root field resolves once per request. Any ResolveBulk
    /// overload on the same field satisfies the rule, chained or on a stored builder.
    /// </summary>
    private static void ReportMissingBulkResolver(OperationAnalysisContext context, IInvocationOperation invocation, IMethodSymbol method, string fieldName, Location location)
    {
        if (EntityGraphQLApi.IsRootField(invocation))
            return;
        if (EntityGraphQLApi.FieldBuilderAlsoCalls(invocation, "ResolveBulk"))
            return;

        // the owner type is the field builder's context type - FieldToResolve<TContext> etc.
        var ownerType = method.ContainingType.TypeArguments.FirstOrDefault();
        if (ownerType == null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.ServiceFieldWithoutBulkResolver, location, fieldName, ownerType.Name));
    }

    /// <summary>
    /// EGQL002. Async fields on a list resolve concurrently (MaxQueryConcurrency defaults to 100), so a
    /// service that does not support concurrent use will fail. Satisfied by passing maxConcurrency: 1.
    /// </summary>
    private static void ReportUnsafeService(OperationAnalysisContext context, IInvocationOperation invocation, IMethodSymbol method, string fieldName, Location location)
    {
        var unsafeService = method.TypeArguments.FirstOrDefault(IsKnownNonThreadSafeService);
        if (unsafeService == null)
            return;
        // only serialising the resolves makes a non-thread-safe service safe - any other limit still runs
        // them concurrently, just fewer at a time
        if (invocation.Arguments.Any(a => a.Parameter?.Name == "maxConcurrency" && a.ArgumentKind == ArgumentKind.Explicit && IsOne(a.Value)))
            return;

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsafeServiceInAsyncField, location, fieldName, unsafeService.Name));
    }

    /// <summary>
    /// The argument is the literal 1. The parameter is <c>int?</c>, so the literal arrives wrapped in the
    /// int-to-int? conversion, which is not itself a constant.
    /// </summary>
    private static bool IsOne(IOperation value)
    {
        var current = value;
        while (current is IConversionOperation conversion)
            current = conversion.Operand;
        return current.ConstantValue is { HasValue: true, Value: 1 };
    }

    /// <summary>
    /// Services documented as unsafe for concurrent use. Only EF Core's DbContext for now - it is by far
    /// the common case and Microsoft documents the constraint explicitly.
    /// </summary>
    private static bool IsKnownNonThreadSafeService(ITypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.Name == "DbContext" && current.ContainingNamespace?.ToDisplayString() == "Microsoft.EntityFrameworkCore")
                return true;
        }
        return false;
    }
}
