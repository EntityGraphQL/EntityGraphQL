using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using EntityQueryLanguage.DataApi.Parsing;
using EntityQueryLanguage.DataApi.Util;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Util;

namespace EntityQueryLanguage.DataApi
{
    public class EfRelationHandler : IRelationHandler
    {
        private Type _lookupType;
        private List<LambdaExpression> _includes = new List<LambdaExpression>();

        public EfRelationHandler(Type lookupType)
        {
            _lookupType = lookupType;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="rootSelect">The top level expression we are calling .Select() on. For EF .Include() need to be added here</param>
        /// <param name="fieldExpressions">The fields they are selecting</param>
        /// <param name="contextParameter">Current context</param>
        /// <param name="exp">Current expression. Not used for EF, but it may be an inner select</param>
        /// <param name="name"></param>
        /// <param name="schemaProvider"></param>
        /// <returns></returns>
        public LambdaExpression BuildNodeForSelect(List<Expression> relationFields, ParameterExpression contextParameter, LambdaExpression exp, string name, ISchemaProvider schemaProvider)
        {
            var body = exp.Body;
            foreach (var relation in relationFields)
            {
                // we want to capture the relations here to process later.
                var rLambda = Expression.Lambda(relation, contextParameter);
                _includes.Add(rLambda);
                // body = ExpressionUtil.MakeExpressionCall(new Type[1] { _lookupType }, "Include", new Type[2] { body.Type, relation.RelationExpression.Type }, body, rLambda);
            }
            return exp;
        }

        public LambdaExpression HandleSelectComplete(LambdaExpression baseExpression)
        {
            var exp = baseExpression.Body;
            _includes.Reverse();
            var type = exp.Type.GetGenericArguments()[0];
            foreach (var relationLambda in _includes)
            {
                if (type != relationLambda.Parameters.First().Type)
                {
                    exp = ExpressionUtil.MakeExpressionCall(new Type[] { _lookupType }, "ThenInclude", new Type[] { type, relationLambda.Parameters.First().Type, relationLambda.Body.Type }, exp, relationLambda);
                }
                else
                {
                    exp = ExpressionUtil.MakeExpressionCall(new Type[1] { _lookupType }, "Include", new Type[2] { relationLambda.Parameters.First().Type, relationLambda.Body.Type }, exp, relationLambda);
                }
            }
            var lambda = Expression.Lambda(exp, baseExpression.Parameters);
            return lambda;
        }
    }
}