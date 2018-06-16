using System;
using System.Collections.Generic;
using System.Linq.Expressions;

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

        public LambdaExpression Expression { get; private set; }
        public Type Type { get { return Expression.Type; } }

        public IEnumerable<object> ConstantParameterValues { get { return constantParameterValues; } }

        public Type BodyType { get { return Expression.Body.Type; } }

        public QueryResult(LambdaExpression compiledEql, IEnumerable<object> parameterValues)
        {
            Expression = compiledEql;
            this.constantParameterValues = parameterValues;
        }
        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (constantParameterValues != null)
                allArgs.AddRange(constantParameterValues);
            return Expression.Compile().DynamicInvoke(allArgs.ToArray());
        }
        public TObject Execute<TObject>(params object[] args)
        {
            return (TObject)Execute(args);
        }
    }
}
