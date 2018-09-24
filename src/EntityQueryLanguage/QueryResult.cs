using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using EntityQueryLanguage.Compiler;

namespace EntityQueryLanguage
{
    /// <summary>
    /// Represents the final result of a single Expression that can be executed.
    ///
    /// Note GraphQL is 1 of these per root field.
    ///
    /// The LambdaExpression will be (context, constParameters, ...) where context is required to be passed in when your call Execute()
    /// </summary>
    public class QueryResult
    {
        private readonly IEnumerable<object> constantParameterValues;
        private readonly ExpressionResult expressionResult;
        private readonly List<ParameterExpression> contextParams;

        public LambdaExpression LambdaExpression { get { return Expression.Lambda(expressionResult.Expression, contextParams.ToArray()); } }
        public Type Type { get { return LambdaExpression.Type; } }

        public IEnumerable<object> ConstantParameterValues { get { return constantParameterValues; } }

        public Type BodyType { get { return LambdaExpression.Body.Type; } }

        public ExpressionResult ExpressionResult { get { return expressionResult; } }

        public bool IsMutation { get { return typeof(MutationResult) == expressionResult.GetType(); } }

        public QueryResult(ExpressionResult expressionResult, List<ParameterExpression> contextParams, IEnumerable<object> parameterValues)
        {
            this.expressionResult = expressionResult;
            this.contextParams = contextParams;
            this.constantParameterValues = parameterValues;
        }
        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (constantParameterValues != null)
                allArgs.AddRange(constantParameterValues);
            return LambdaExpression.Compile().DynamicInvoke(allArgs.ToArray());
        }
    }
}
