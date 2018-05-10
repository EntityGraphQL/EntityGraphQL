using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityQueryLanguage.DataApi.Parsing;
using EntityQueryLanguage.DataApi.Util;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage.DataApi
{
    public class EfRelationHandler : IRelationHandler
    {
        private Type _lookupType;

        public EfRelationHandler(Type lookupType)
        {
            _lookupType = lookupType;
        }

        public DataApiNode BuildNode(List<DataApiNode> fieldExpressions, ParameterExpression contextParameter, LambdaExpression exp, string name, ISchemaProvider schemaProvider)
        {
            var localFields = fieldExpressions.Where(f => f.Expression.NodeType != ExpressionType.MemberInit && f.Expression.NodeType != ExpressionType.Call);
            var relations = fieldExpressions.Where(f => f.Expression.NodeType == ExpressionType.MemberInit || f.Expression.NodeType == ExpressionType.Call);

            var body = exp.Body;
            foreach (var relation in relations)
            {
                var rLambda = Expression.Lambda(relation.RelationExpression, contextParameter);
                body = ExpressionUtil.MakeExpressionCall(new Type[1] { _lookupType }, "Include", new Type[2] { contextParameter.Type, relation.RelationExpression.Type }, body, rLambda);
            }

            var selectExpression = DataApiExpressionUtil.SelectDynamic(contextParameter, body, fieldExpressions, schemaProvider);
            var node = new DataApiNode(name, selectExpression, exp.Parameters.Any() ? exp.Parameters.First() : null, exp.Body, relations);
            return node;
        }
    }
}