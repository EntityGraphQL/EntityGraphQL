using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Represents the final result of a single Expression that can be executed.
    ///
    /// The LambdaExpression will be (context, constParameters, ...) where context is required to be passed in when your call Execute()
    /// </summary>
    public class CompiledQueryResult
    {
        private readonly List<ParameterExpression> contextParams;

        public LambdaExpression LambdaExpression { get { return Expression.Lambda(ExpressionResult.Expression, ContextParams.Concat(ExpressionResult.ConstantParameters.Keys).ToArray()); } }

        public IReadOnlyDictionary<ParameterExpression, object> ConstantParameters { get { return ExpressionResult.ConstantParameters; } }

        public Type BodyType { get { return LambdaExpression.Body.Type; } }

        public ExpressionResult ExpressionResult { get; private set; }

        public bool IsMutation { get { return typeof(MutationResult) == ExpressionResult.GetType(); } }

        public List<ParameterExpression> ContextParams { get { return contextParams; } }

        public CompiledQueryResult(ExpressionResult expressionResult, List<ParameterExpression> contextParams)
        {
            this.ExpressionResult = expressionResult;
            this.contextParams = contextParams;
        }
        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (ConstantParameters != null)
            {
                allArgs.AddRange(ConstantParameters.Values);
            }
            return LambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
        }
    }
}
