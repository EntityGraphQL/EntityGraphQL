using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// Analyzes how schema elements are declared:
/// EGQL005 - a name that is not a valid GraphQL name.
/// EGQL006 - a [GraphQLSubscription] method that does not return an observable.
/// EGQL007 - a [GraphQLOneOf] input type with a non-nullable field.
/// EGQL008 - the same field added to a type twice.
/// EGQL009 - a blocking execute call inside an async method.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SchemaDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Same rule EntityGraphQL applies to schema type names.</summary>
    private static readonly Regex ValidName = new("^[_a-zA-Z0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string SubscriptionAttribute = "EntityGraphQL.Schema.GraphQLSubscriptionAttribute";
    private const string OneOfAttribute = "EntityGraphQL.Schema.Directives.GraphQLOneOfAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.InvalidGraphQLName,
            DiagnosticDescriptors.SubscriptionMustReturnObservable,
            DiagnosticDescriptors.OneOfFieldsMustBeNullable,
            DiagnosticDescriptors.DuplicateFieldName,
            DiagnosticDescriptors.UseAsyncExecuteRequest
        );

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterOperationBlockAction(AnalyzeBlockForDuplicateFields);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!EntityGraphQLApi.IsEntityGraphQLType(invocation.TargetMethod.ContainingType))
            return;

        ReportInvalidNames(context, invocation);
        ReportBlockingExecute(context, invocation);
    }

    /// <summary>EGQL005 - any literal bound to a "name" parameter must be a valid GraphQL name.</summary>
    private static void ReportInvalidNames(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name != "name")
                continue;
            if (argument.Value.ConstantValue is not { HasValue: true, Value: string name })
                continue;
            if (ValidName.IsMatch(name))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.InvalidGraphQLName, argument.Value.Syntax.GetLocation(), name));
        }
    }

    /// <summary>EGQL009 - ExecuteRequest / ExecuteRequestWithContext called inside an async method.</summary>
    private static void ReportBlockingExecute(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var name = invocation.TargetMethod.Name;
        if (name is not ("ExecuteRequest" or "ExecuteRequestWithContext"))
            return;
        // only worth saying inside an async method, where awaiting the Async overload is free
        if (context.ContainingSymbol is not IMethodSymbol { IsAsync: true })
            return;

        context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UseAsyncExecuteRequest, EntityGraphQLApi.MethodNameLocation(invocation), name));
    }

    /// <summary>EGQL006 - a subscription must return IObservable&lt;T&gt; (optionally wrapped in a task).</summary>
    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (!HasAttribute(method, SubscriptionAttribute))
            return;

        var returnType = method.ReturnType;
        // unwrap Task<T> / ValueTask<T>
        if (returnType is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments.Length: 1 } task)
            returnType = task.TypeArguments[0];

        if (IsObservable(returnType))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(DiagnosticDescriptors.SubscriptionMustReturnObservable, method.Locations.FirstOrDefault(), method.Name, method.ReturnType.ToDisplayString())
        );
    }

    /// <summary>EGQL007 - every field of a OneOf input type must be nullable.</summary>
    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!HasAttribute(type, OneOfAttribute))
            return;

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic || IsNullable(property.Type))
                continue;

            context.ReportDiagnostic(
                Diagnostic.Create(DiagnosticDescriptors.OneOfFieldsMustBeNullable, property.Locations.FirstOrDefault(), type.Name, property.Name)
            );
        }
    }

    /// <summary>
    /// EGQL008 - the same field name added twice to the same type within one method body. Scoped to a single
    /// body so unrelated schema-building code (or conditional branches in different methods) is not flagged.
    /// </summary>
    private static void AnalyzeBlockForDuplicateFields(OperationBlockAnalysisContext context)
    {
        var seen = new Dictionary<(ITypeSymbol? Owner, string Name), IInvocationOperation>();

        foreach (var block in context.OperationBlocks)
        {
            foreach (var invocation in block.Descendants().OfType<IInvocationOperation>())
            {
                if (invocation.TargetMethod.Name != "AddField" || !EntityGraphQLApi.IsEntityGraphQLType(invocation.TargetMethod.ContainingType))
                    continue;

                var nameArgument = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "name");
                if (nameArgument?.Value.ConstantValue is not { HasValue: true, Value: string fieldName })
                    continue;

                var owner = invocation.TargetMethod.ContainingType.TypeArguments.FirstOrDefault();
                var key = (owner, fieldName);
                if (seen.ContainsKey(key))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DuplicateFieldName, EntityGraphQLApi.MethodNameLocation(invocation), fieldName));
                }
                else
                {
                    seen[key] = invocation;
                }
            }
        }
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == metadataName);

    private static bool IsObservable(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "IObservable" } || type.AllInterfaces.Any(i => i.Name == "IObservable" && i.ContainingNamespace?.ToDisplayString() == "System");

    /// <summary>
    /// A field is nullable when it is Nullable&lt;T&gt; or a reference type annotated as nullable. Reference
    /// types in a nullable-disabled context are "oblivious" - treated as nullable so we do not report there.
    /// </summary>
    private static bool IsNullable(ITypeSymbol type)
    {
        if (type.IsValueType)
            return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        return type.NullableAnnotation != NullableAnnotation.NotAnnotated;
    }
}
