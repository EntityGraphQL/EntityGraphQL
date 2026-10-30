using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler;

/// <summary>
/// An object or list literal argument with a variable inside it - <c>{ status: $status }</c>. It can only be
/// converted to the argument's type once the variables are known, so the parser keeps it as written.
/// </summary>
internal sealed class LiteralWithVariables(ISchemaProvider schema, object value, Type targetType)
{
    public static bool Contains(object? value) =>
        value switch
        {
            Expression or VariableReference => true,
            Dictionary<string, object?> dict => dict.Values.Any(Contains),
            List<object?> list => list.Any(Contains),
            _ => false,
        };

    /// <summary>The arguments with any literal holding variables built from the variables' values</summary>
    public static IReadOnlyDictionary<string, object?> ResolveAll(IReadOnlyDictionary<string, object?> args, ParameterExpression? docParam, IArgumentsTracker? docVariables) =>
        args.Values.Any(v => v is LiteralWithVariables)
            ? args.ToDictionary(kv => kv.Key, kv => kv.Value is LiteralWithVariables literal ? literal.Resolve(docParam, docVariables) : kv.Value)
            : args;

    public object? Resolve(ParameterExpression? docParam, IArgumentsTracker? docVariables)
    {
        if (docParam == null || docVariables == null)
            throw new EntityGraphQLException(GraphQLErrorCategory.ExecutionError, "Variables used in an argument value but no variables were supplied");
        return GraphQLParser.ConvertArgumentValue(schema, Substitute(value, docParam, docVariables), targetType);
    }

    private static object? Substitute(object? value, ParameterExpression docParam, IArgumentsTracker docVariables) =>
        value switch
        {
            VariableReference varRef => Substitute(Expression.PropertyOrField(docParam, varRef.VariableName), docParam, docVariables),
            Expression exp => Expression.Lambda(exp, docParam).Compile().DynamicInvoke([docVariables]),
            // a field given a variable that was not sent is left out, as if not written - its default applies
            Dictionary<string, object?> dict => dict.Where(kv => IsSent(kv.Value, docVariables)).ToDictionary(kv => kv.Key, kv => Substitute(kv.Value, docParam, docVariables)),
            List<object?> list => list.ConvertAll(v => Substitute(v, docParam, docVariables)),
            _ => value,
        };

    private static bool IsSent(object? value, IArgumentsTracker docVariables) =>
        value switch
        {
            VariableReference varRef => docVariables.IsSet(varRef.VariableName),
            MemberExpression variable => docVariables.IsSet(variable.Member.Name),
            _ => true,
        };
}
