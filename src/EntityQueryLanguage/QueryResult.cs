using System.Collections.Generic;
using System.Linq.Expressions;

namespace EntityQueryLanguage
{
    public class QueryResult
    {
        private readonly IEnumerable<object> parameterValues;

        public LambdaExpression Expression { get; private set; }
        public QueryResult(LambdaExpression compiledEql, IEnumerable<object> parameterValues)
        {
            Expression = compiledEql;
            this.parameterValues = parameterValues;
        }
        public object Execute(params object[] args)
        {
            var allArgs = new List<object>(args);
            if (parameterValues != null)
                allArgs.AddRange(parameterValues);
            return Expression.Compile().DynamicInvoke(allArgs.ToArray());
        }
        public TObject Execute<TObject>(params object[] args)
        {
            return (TObject)Execute(args);
        }
    }
}
