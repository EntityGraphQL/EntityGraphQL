using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions
{
    internal class ConnectionEdgeNodeExtension : BaseFieldExtension
    {
        private readonly ConnectionEdgeExtension edgeExtension;
        public ParameterExpression SelectParam { get; set; }
        public ParameterExpression EdgeParam { get; set; }

        public ConnectionEdgeNodeExtension(ConnectionEdgeExtension edgeExtension, ParameterExpression selectParam)
        {
            this.edgeExtension = edgeExtension;
            this.SelectParam = selectParam;
        }

        public override (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            var selection = new Dictionary<string, Expression>();
            var newEdgeParam = EdgeParam;
            if (newEdgeParam == null)
            {
                foreach (var item in selectionExpressions)
                {
                    var exp = item.Value.Expression;
                    exp = parameterReplacer.ReplaceByType(item.Value.Expression, baseExpression.Type, SelectParam);
                    selection[item.Key] = exp;
                    item.Value.Expression = exp;
                }
                var newExp = ExpressionUtil.CreateNewExpression(selectionExpressions.ToDictionary(i => i.Key, i => i.Value.Expression), out Type anonType);
                newEdgeParam = Expression.Parameter(typeof(ConnectionEdge<>).MakeGenericType(anonType), "newEdgeParam");
                edgeExtension.SetNodeExpression(newExp, anonType, newEdgeParam);

                var newBaseExpression = Expression.PropertyOrField(newEdgeParam, "Node");

                foreach (var item in selectionExpressions)
                {
                    item.Value.Expression = Expression.PropertyOrField(newBaseExpression, item.Key);
                }
                return (newBaseExpression, selectionExpressions, selectContextParam);
            }

            // The T in ConnectionEdge<T> will change because we move the nodeExpression Select back. But the Edge Node fields
            // have already been visited so we need to rebuild them
            baseExpression = Expression.PropertyOrField(newEdgeParam, "Node");

            return (baseExpression, selectionExpressions, selectContextParam);
        }
    }
}