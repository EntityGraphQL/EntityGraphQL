using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.Connections;

namespace EntityGraphQL.Schema.FieldExtensions
{
    internal class ConnectionEdgeExtension : IFieldExtension
    {
        private Expression nodeExpression;
        private Type nodeExpressionType;
        private Type newEdgeType;
        internal ParameterExpression ArgExpression { get; set; }
        private readonly ConnectionPagingExtension connectionPagingExtension;
        private readonly Type listType;
        private readonly Type edgeType;
        private readonly ParameterExpression firstSelectParam;

        public ConnectionEdgeExtension(ConnectionPagingExtension connectionPagingExtension, Type listType, Type edgeType, ParameterExpression firstSelectParam)
        {
            this.connectionPagingExtension = connectionPagingExtension;
            this.listType = listType;
            this.edgeType = edgeType;
            this.firstSelectParam = firstSelectParam;
        }

        public void Configure(ISchemaProvider schema, Field field)
        {
        }

        public Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            return connectionPagingExtension.EdgeExpression;
        }

        public (ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionPreSelection(GraphQLFieldType fieldType, ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            var selectParam = Expression.Parameter(nodeExpressionType);
            var idxParam = Expression.Parameter(typeof(int));
            var edgesExp = (ExpressionResult)
            Expression.Call(typeof(Enumerable), "Select", new Type[] { nodeExpressionType, newEdgeType },
                Expression.Call(typeof(Enumerable), "ToList", new Type[] { nodeExpressionType },
                    Expression.Call(typeof(Queryable), "Select", new Type[] { listType, nodeExpressionType },
                        connectionPagingExtension.EdgeExpression,
                        Expression.Lambda(nodeExpression, firstSelectParam)
                    )
                ),
                Expression.Lambda(
                    Expression.MemberInit(Expression.New(newEdgeType),
                        new List<MemberBinding>
                        {
                            Expression.Bind(newEdgeType.GetProperty("Node"), selectParam),
                            Expression.Bind(newEdgeType.GetProperty("Cursor"), Expression.Call(typeof(ConnectionPagingExtension), "GetCursor", null, ArgExpression, idxParam)),
                        }
                    ),
                    selectParam,
                    idxParam
                )
            );
            edgesExp.AddServices(baseExpression.Services);
            edgesExp.AddConstantParameters(baseExpression.ConstantParameters);
            return (edgesExp, selectionExpressions, Expression.Parameter(newEdgeType));
        }

        public ExpressionResult ProcessFinalExpression(GraphQLFieldType fieldType, ExpressionResult expression, ParameterReplacer parameterReplacer)
        {
            return expression;
        }

        internal void SetNodeExpression(Expression nodeExpression, Type nodeExpressionType)
        {
            this.nodeExpression = nodeExpression;
            this.nodeExpressionType = nodeExpressionType;
            this.newEdgeType = typeof(ConnectionEdge<>).MakeGenericType(nodeExpressionType);
        }
    }
}