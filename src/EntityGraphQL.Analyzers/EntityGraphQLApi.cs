using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace EntityGraphQL.Analyzers;

/// <summary>
/// Shared helpers for recognising EntityGraphQL's fluent schema-building API in user code.
/// Everything is matched by symbol so the analyzer no-ops cleanly in projects that do not reference
/// EntityGraphQL (and never guesses from identifier names alone).
/// </summary>
internal static class EntityGraphQLApi
{
    private const string RootNamespace = "EntityGraphQL";

    /// <summary>True when the symbol is declared in the EntityGraphQL assembly/namespace tree.</summary>
    internal static bool IsEntityGraphQLType(ITypeSymbol? type)
    {
        for (var ns = type?.ContainingNamespace; ns != null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            if (ns.ContainingNamespace?.IsGlobalNamespace == true)
                return ns.Name == RootNamespace;
        }
        return false;
    }

    /// <summary>
    /// The chain of invocations a fluent call is built from, receiver-first
    /// (e.g. for <c>schema.Type&lt;P&gt;().AddField(..).Resolve&lt;S&gt;(..)</c> this yields AddField then Type).
    /// </summary>
    internal static IEnumerable<IInvocationOperation> ReceiverChain(IInvocationOperation invocation)
    {
        var current = Receiver(invocation);
        while (current != null)
        {
            // step through conversions the compiler inserts on fluent returns
            if (current is IConversionOperation conversion)
            {
                current = conversion.Operand;
                continue;
            }
            if (current is not IInvocationOperation inv)
                yield break;
            yield return inv;
            current = Receiver(inv);
        }
    }

    /// <summary>
    /// What the call was made on. Extension methods (the Use* field extensions) have no Instance - their
    /// receiver is the first argument.
    /// </summary>
    internal static IOperation? Receiver(IInvocationOperation invocation)
    {
        if (invocation.Instance != null)
            return invocation.Instance;
        if (invocation.TargetMethod.IsExtensionMethod && invocation.Arguments.Length > 0)
            return invocation.Arguments[0].Value;
        return null;
    }

    /// <summary>
    /// The type a field's expression produces, from the first Expression&lt;Func&lt;..&gt;&gt; parameter of an
    /// AddField/ReplaceField overload (its last generic argument is the return type). Null when the field
    /// is declared without an expression (AddField(name, description) with a later Resolve).
    /// </summary>
    internal static ITypeSymbol? FieldExpressionReturnType(IInvocationOperation invocation)
    {
        foreach (var parameter in invocation.TargetMethod.Parameters)
        {
            if (parameter.Type is not INamedTypeSymbol { Name: "Expression", TypeArguments.Length: 1 } expression)
                continue;
            if (expression.TypeArguments[0] is INamedTypeSymbol { TypeArguments.Length: > 0 } func)
                return func.TypeArguments[func.TypeArguments.Length - 1];
        }
        return null;
    }

    /// <summary>
    /// The lambda an argument was written as, unwrapping the conversion/delegate-creation the compiler puts
    /// around it. Null when the argument is anything else (a method group, a variable, ...).
    /// </summary>
    internal static IAnonymousFunctionOperation? Lambda(IArgumentOperation argument)
    {
        var value = argument.Value;
        while (true)
        {
            if (value is IConversionOperation conversion)
                value = conversion.Operand;
            else if (value is IDelegateCreationOperation delegateCreation)
                value = delegateCreation.Target;
            else if (value is IParenthesizedOperation parenthesized)
                value = parenthesized.Operand;
            else
                break;
        }
        return value as IAnonymousFunctionOperation;
    }

    /// <summary>
    /// The type of a lambda argument's body, before any implicit conversion the parameter's declared type
    /// forces (Resolve takes Expression&lt;Func&lt;.., object?&gt;&gt;, so the declared type says nothing).
    /// </summary>
    internal static ITypeSymbol? LambdaBodyType(IArgumentOperation argument)
    {
        if (Lambda(argument) is not { } lambda)
            return null;

        IOperation? returned = null;
        foreach (var operation in lambda.Body.Operations)
        {
            if (operation is IReturnOperation { ReturnedValue: not null } ret)
            {
                returned = ret.ReturnedValue;
                break;
            }
            if (operation is IExpressionStatementOperation statement)
            {
                returned = statement.Operation;
                break;
            }
        }

        while (returned is IConversionOperation { IsImplicit: true } implicitConversion)
            returned = implicitConversion.Operand;
        return returned?.Type;
    }

    /// <summary>True for a type GraphQL treats as a list (and not a string, which is IEnumerable&lt;char&gt;).</summary>
    internal static bool IsCollection(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return false;
        if (type.TypeKind == TypeKind.Array)
            return true;
        // the type itself may be the enumerable interface (IEnumerable<T>, IQueryable<T>,
        // IOrderedQueryable<T>, ...) or implement it
        return IsEnumerableInterface(type) || type.AllInterfaces.Any(IsEnumerableInterface);
    }

    /// <summary>
    /// Checks OriginalDefinition, not the constructed symbol - Roslyn only sets SpecialType on the unbound
    /// definition, so IEnumerable&lt;Person&gt;.SpecialType is None while IEnumerable&lt;T&gt;'s is not.
    /// </summary>
    private static bool IsEnumerableInterface(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType is SpecialType.System_Collections_Generic_IEnumerable_T or SpecialType.System_Collections_IEnumerable;

    /// <summary>True for Task&lt;T&gt; / ValueTask&lt;T&gt; (and the non-generic Task).</summary>
    internal static bool IsAwaitable(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task" or "ValueTask" } named && named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";

    /// <summary>
    /// True when the field being built belongs to the Query or Mutation root type rather than an entity
    /// type - a root field resolves once per request, so per-item concerns do not apply to it.
    /// </summary>
    internal static bool IsRootField(IInvocationOperation invocation)
    {
        foreach (var link in ReceiverChain(invocation))
        {
            if (!IsEntityGraphQLType(link.TargetMethod.ContainingType))
                continue;
            var name = link.TargetMethod.Name;
            if (name is "Query" or "Mutation" or "Subscription")
                return true;
        }
        // schema.UpdateQuery(query => query.AddField(..)) - the receiver chain bottoms out at the lambda
        // parameter, so the enclosing call is the only thing that says this is the root type
        for (var parent = invocation.Parent; parent != null; parent = parent.Parent)
        {
            if (parent is IInvocationOperation { TargetMethod.Name: "UpdateQuery" } enclosing && IsEntityGraphQLType(enclosing.TargetMethod.ContainingType))
                return true;
        }
        return false;
    }

    /// <summary>
    /// The GraphQL field name from the AddField/ReplaceField call this chain is built on, when it is a
    /// literal. Returns null when the name is computed.
    /// </summary>
    internal static string? FieldNameFromChain(IInvocationOperation invocation)
    {
        foreach (var link in ReceiverChain(invocation))
        {
            if (link.TargetMethod.Name is not ("AddField" or "ReplaceField"))
                continue;
            var firstArg = link.Arguments.FirstOrDefault(a => a.Parameter?.Ordinal == 0);
            if (firstArg?.Value.ConstantValue is { HasValue: true, Value: string name })
                return name;
        }
        return null;
    }

    /// <summary>
    /// True when any method whose name starts with <paramref name="namePrefix"/> is called anywhere in the
    /// statement containing this call. Used to spot other links of the same fluent chain (which may appear
    /// before or after this call, so the receiver chain alone is not enough).
    /// </summary>
    internal static bool StatementCallsMethod(IInvocationOperation invocation, string namePrefix)
    {
        var statement = invocation.Syntax.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null)
            return false;
        return statement.DescendantNodes().OfType<SimpleNameSyntax>().Any(n => n.Identifier.ValueText.StartsWith(namePrefix, System.StringComparison.Ordinal));
    }

    /// <summary>The location of just the method name, so the squiggle sits under <c>Resolve</c> not the whole chain.</summary>
    internal static Location MethodNameLocation(IInvocationOperation invocation)
    {
        if (invocation.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member })
            return member.Name.GetLocation();
        return invocation.Syntax.GetLocation();
    }
}
