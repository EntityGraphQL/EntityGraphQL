using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Compiler.EntityQuery;

/// <summary>
/// Represents the final result of a single Expression that can be executed.
///
/// The LambdaExpression will be (context, constParameters, ...) where context is required to be passed in when your call Execute()
/// </summary>
public class CompiledQueryResult
{
    public LambdaExpression LambdaExpression => Expression.Lambda(ExpressionResult, ContextParams.Concat(ConstantParameters.Keys).ToArray());

    public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get; } = new Dictionary<ParameterExpression, object>();

    public Expression ExpressionResult { get; private set; }

    public List<ParameterExpression> ContextParams { get; }

    public CompiledQueryResult(Expression expressionResult, List<ParameterExpression> contextParams)
    {
        this.ExpressionResult = expressionResult;
        this.ContextParams = contextParams;
    }

    public object? Execute(params object[] args)
    {
        var allArgs = new List<object>(args);
        allArgs.AddRange(ConstantParameters.Values);
        return LambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
    }
}
