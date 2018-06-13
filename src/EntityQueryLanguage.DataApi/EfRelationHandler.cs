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
        public Expression BuildNodeForSelect(List<Expression> relationFields, ParameterExpression contextParameter, Expression exp, string name, ISchemaProvider schemaProvider)
        {
            var body = exp;
            foreach (var relation in relationFields)
            {
                // we want to capture the relations here to process later.
                var rLambda = Expression.Lambda(relation, contextParameter);
                _includes.Add(rLambda);
                // body = ExpressionUtil.MakeExpressionCall(new Type[1] { _lookupType }, "Include", new Type[2] { body.Type, relation.RelationExpression.Type }, body, rLambda);
            }
            return exp;
        }

        public Expression HandleSelectComplete(Expression baseExpression)
        {
            var exp = baseExpression;
            _includes.Reverse();
            var type = exp.Type.GetGenericArguments()[0];
            Type lastType = null;
            foreach (var relationLambda in _includes)
            {
                var relationParamType = relationLambda.Parameters.First().Type;
                if (type != relationParamType)
                {
                    // EF requires something like this
                    // .Include(level1 => level1.Level2)
                    //     .ThenInclude(level2 => level2.level3)
                    // .Include(level1 => level1.Level2b)
                    //     .ThenInclude(level2b => level2b.Level3b)
                    //         .ThenInclude(level3b => level3b.Level4)
                    if (lastType != null && relationParamType != lastType)
                    {
                        exp = InsertTopLevelIncludesIfRequired(exp, type, relationParamType);
                    }
                    exp = ExpressionUtil.MakeExpressionCall(new Type[] { _lookupType }, "ThenInclude", new Type[] { type, relationParamType, relationLambda.Body.Type }, exp, relationLambda);
                }
                else
                {
                    exp = ExpressionUtil.MakeExpressionCall(new Type[1] { _lookupType }, "Include", new Type[2] { relationParamType, relationLambda.Body.Type }, exp, relationLambda);
                }
                lastType = relationLambda.Body.Type.IsEnumerable() ? relationLambda.Body.Type.GetGenericArguments()[0] : relationLambda.Body.Type;
            }
            return exp;
        }

        private Expression InsertTopLevelIncludesIfRequired(Expression exp, Type rootType, Type relationParamType)
        {
            var searchParamType = relationParamType;
            var lastRelation = _includes.Where(r => (r.ReturnType.IsEnumerable() ? r.ReturnType.GetGenericArguments()[0] : r.ReturnType) == searchParamType).First();

            if (lastRelation.Parameters.First().Type != rootType)
            {
                exp = InsertTopLevelIncludesIfRequired(exp, rootType, lastRelation.Parameters.First().Type);
            }

            var newRelationParamType = lastRelation.Parameters.First().Type;
            if (newRelationParamType == rootType)
            {
                exp = ExpressionUtil.MakeExpressionCall(new Type[] { _lookupType }, "Include", new Type[] { rootType, lastRelation.Body.Type }, exp, lastRelation);
            }
            else
            {
                exp = ExpressionUtil.MakeExpressionCall(new Type[] { _lookupType }, "ThenInclude", new Type[] { rootType, newRelationParamType, lastRelation.Body.Type }, exp, lastRelation);
            }
            searchParamType = lastRelation.Parameters.First().Type;

            return exp;
        }
    }
}