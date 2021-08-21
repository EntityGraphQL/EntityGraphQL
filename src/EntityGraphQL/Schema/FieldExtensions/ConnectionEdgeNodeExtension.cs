using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.Connections;

namespace EntityGraphQL.Schema.FieldExtensions
{
    internal class ConnectionEdgeNodeExtension : IFieldExtension
    {
        private readonly ConnectionEdgeExtension edgeExtension;
        private readonly ParameterExpression selectParam;

        public ConnectionEdgeNodeExtension(ConnectionEdgeExtension edgeExtension, ParameterExpression selectParam)
        {
            this.edgeExtension = edgeExtension;
            this.selectParam = selectParam;
        }

        public void Configure(ISchemaProvider schema, Field field)
        {
        }

        public Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            return expression;
        }

        public (ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionPreSelection(GraphQLFieldType fieldType, ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            var selection = new Dictionary<string, ExpressionResult>();
            foreach (var item in selectionExpressions)
            {
                var exp = (ExpressionResult)parameterReplacer.ReplaceByType(item.Value.Expression, baseExpression.Type, selectParam);
                exp.AddConstantParameters(item.Value.Expression.ConstantParameters);
                exp.AddServices(item.Value.Expression.Services);
                selection[item.Key] = exp;
                item.Value.Expression = exp;
            }
            var newExp = ExpressionUtil.CreateNewExpression(selectionExpressions.ToDictionary(i => i.Key, i => i.Value.Expression), out Type anonType);
            var newEdgeParam = Expression.Parameter(typeof(ConnectionEdge<>).MakeGenericType(anonType), "newEdgeParam");
            edgeExtension.SetNodeExpression(newExp, anonType, newEdgeParam);

            // The T in ConnectionEdge<T> will change because we move the nodeExpression Select back. But the Edge Node fields
            // have already been visited so we need to rebuild them
            var newBaseExpression = (ExpressionResult)Expression.PropertyOrField(newEdgeParam, "Node");
            newBaseExpression.AddConstantParameters(baseExpression.ConstantParameters);
            newBaseExpression.AddServices(baseExpression.Services);

            foreach (var item in selectionExpressions)
            {
                item.Value.Expression.Expression = Expression.PropertyOrField(newBaseExpression, item.Key);
            }

            return (newBaseExpression, selectionExpressions, selectContextParam);
        }

        public ExpressionResult ProcessFinalExpression(GraphQLFieldType fieldType, ExpressionResult expression, ParameterReplacer parameterReplacer)
        {
            return expression;
        }
    }
}