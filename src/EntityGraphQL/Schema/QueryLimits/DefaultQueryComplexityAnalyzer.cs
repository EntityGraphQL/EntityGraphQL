using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.QueryLimits;

/// <summary>
/// Walks the parsed <see cref="GraphQLDocument"/> tree and computes a cost.
///
/// Cost rules:
/// <list type="bullet">
/// <item>Default field cost is <c>1 + sum(childCost)</c>.</item>
/// <item><c>field.SetComplexity(n)</c> overrides base to <c>n</c> — total becomes <c>n + sum(childCost)</c>.</item>
/// <item><c>field.SetComplexity(ctx =&gt; ...)</c> returns the full cost for that field; it decides whether
///   and how to incorporate children (typically <c>n * (1 + ctx.ChildComplexity)</c>).</item>
/// <item>Fragment spreads and inline fragments pass through — they contribute their contents' cost with
///   no extra base.</item>
/// </list>
///
/// There is no list-size multiplier heuristic. For a field whose cost depends on a <c>first</c>/<c>take</c>
/// argument, set it explicitly: <c>field.SetComplexity(ctx =&gt; ctx.Arg&lt;int&gt;("take") * (1 + ctx.ChildComplexity))</c>.
/// Query variables passed with the request are resolved so <c>take: $pageSize</c> uses the real value.
/// </summary>
public class DefaultQueryComplexityAnalyzer : IQueryComplexityAnalyzer
{
    public int CalculateComplexity(GraphQLDocument document, string? operationName, QueryVariables? variables, ExecutionOptions options)
    {
        var op = string.IsNullOrEmpty(operationName) ? (document.Operations.Count > 0 ? document.Operations[0] : null) : document.Operations.Find(o => o.Name == operationName);
        if (op == null)
            return 0;

        var (docParam, docVariables) = BuildDocVariables(op, variables);

        var total = 0;
        foreach (var field in op.QueryFields)
            total = checked(total + CostOfField(field, document.Fragments, docParam, docVariables));
        return total;
    }

    /// <summary>
    /// Mirrors <c>ExecutableGraphQLStatement.BuildDocumentVariables</c>: populates the dynamic docVars object
    /// from the request's query variables so <c>$var</c> MemberExpression args resolve to their real values.
    /// </summary>
    private static (ParameterExpression? docParam, IArgumentsTracker? docVariables) BuildDocVariables(ExecutableGraphQLStatement op, QueryVariables? variables)
    {
        if (op.OpVariableParameter == null || op.OpDefinedVariables.Count == 0)
            return (null, null);

        var docVars = (IArgumentsTracker)Activator.CreateInstance(op.OpVariableParameter.Type)!;
        foreach (var (name, argType) in op.OpDefinedVariables)
        {
            if (variables == null || !variables.TryGetValue(name, out var raw))
            {
                if (!argType.DefaultValue.IsSet)
                    continue; // not provided, no default — leave as CLR default (0 / null)
                raw = argType.DefaultValue.Value;
            }

            var val = ExpressionUtil.ConvertObjectType(raw, argType.RawType, op.Schema);
            op.OpVariableParameter.Type.GetField(name)?.SetValue(docVars, val);
            docVars.MarkAsSet(name);
        }

        return (op.OpVariableParameter, docVars);
    }

    private static int CostOfField(BaseGraphQLField field, IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments, ParameterExpression? docParam, IArgumentsTracker? docVariables)
    {
        if (field is GraphQLFragmentSpreadField spread)
        {
            if (!fragments.TryGetValue(spread.Name, out var fragment))
                return 0;
            return SumChildren(fragment.QueryFields, fragments, docParam, docVariables);
        }

        if (field is GraphQLInlineFragmentField inline)
            return SumChildren(inline.QueryFields, fragments, docParam, docVariables);

        var childCost = SumChildren(field.QueryFields, fragments, docParam, docVariables);
        var ext = field.Field != null ? FieldComplexityLookup.TryGet(field.Field) : null;

        if (ext is null)
            return checked(1 + childCost);

        if (ext.Calculator != null)
        {
            var resolvedArgs = ResolveArgs(field.Arguments, docParam, docVariables);
            var prebuiltArgs = BuildArgs(field.Field, resolvedArgs);
            return ext.Calculator(new FieldComplexityContext(resolvedArgs, childCost, prebuiltArgs));
        }

        return checked(ext.FixedCost!.Value + childCost);
    }

    private static int SumChildren(List<BaseGraphQLField> fields, IReadOnlyDictionary<string, GraphQLFragmentStatement> fragments, ParameterExpression? docParam, IArgumentsTracker? docVariables)
    {
        var sum = 0;
        for (var i = 0; i < fields.Count; i++)
            sum = checked(sum + CostOfField(fields[i], fragments, docParam, docVariables));
        return sum;
    }

    /// <summary>
    /// Builds the typed args object for the field using the same <see cref="ArgumentUtil"/> path as
    /// execution. Variables are already resolved in <paramref name="resolvedArgs"/> so docParam/docVariables
    /// are passed as null. Validation is skipped (field passed as null).
    /// </summary>
    private static object? BuildArgs(IField? field, IReadOnlyDictionary<string, object?> resolvedArgs)
    {
        if (field?.ArgumentsParameter == null || field.Arguments.Count == 0)
            return null;

        return ArgumentUtil.BuildArgumentsObject(field.Schema, field.Name, null, resolvedArgs, field.Arguments.Values, field.ArgumentsParameter.Type, null, null, new HashSet<string>());
    }

    /// <summary>
    /// Returns a new dictionary with any Expression or VariableReference values replaced by their
    /// resolved values from <paramref name="docVariables"/>. Inline literal args are returned as-is.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> ResolveArgs(IReadOnlyDictionary<string, object?> arguments, ParameterExpression? docParam, IArgumentsTracker? docVariables)
    {
        if (arguments.Count == 0 || docParam == null || docVariables == null)
            return arguments;

        Dictionary<string, object?>? resolved = null;

        foreach (var (name, val) in arguments)
        {
            object? resolvedVal;

            if (val is VariableReference varRef)
            {
                var expr = Expression.PropertyOrField(docParam, varRef.VariableName);
                resolvedVal = Expression.Lambda(expr, docParam).Compile().DynamicInvoke([docVariables]);
            }
            else if (val is Expression argExpr)
            {
                resolvedVal = Expression.Lambda(argExpr, docParam).Compile().DynamicInvoke([docVariables]);
            }
            else
            {
                continue; // inline literal — no change needed
            }

            resolved ??= new Dictionary<string, object?>(arguments);
            resolved[name] = resolvedVal;
        }

        return resolved ?? arguments;
    }
}
