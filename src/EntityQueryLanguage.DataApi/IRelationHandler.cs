using System.Collections.Generic;
using System.Linq.Expressions;
using EntityQueryLanguage.DataApi.Parsing;

namespace EntityQueryLanguage.DataApi
{
    public interface IRelationHandler
    {
        DataApiNode BuildNode(List<DataApiNode> fieldExpressions, ParameterExpression contextParameter, LambdaExpression exp, string name, ISchemaProvider schemaProvider);
    }
}
