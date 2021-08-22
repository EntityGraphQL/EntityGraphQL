using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Schema.FieldExtensions
{
    internal class ConnectionEdgeExtension : BaseFieldExtension
    {
        private Expression nodeExpression;
        private Type nodeExpressionType;
        private ParameterExpression newEdgeParam;
        private Type newEdgeType;

        internal ParameterExpression ArgExpression { get; set; }
        private readonly ConnectionPagingExtension connectionPagingExtension;
        private readonly Type listType;
        private readonly ParameterExpression firstSelectParam;
        private readonly bool isQueryable;

        public ConnectionEdgeExtension(ConnectionPagingExtension connectionPagingExtension, Type listType, ParameterExpression firstSelectParam, bool isQueryable)
        {
            this.connectionPagingExtension = connectionPagingExtension;
            this.listType = listType;
            this.firstSelectParam = firstSelectParam;
            this.isQueryable = isQueryable;
        }

        public override Expression GetExpression(Field field, ExpressionResult expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            return connectionPagingExtension.EdgeExpression;
        }

        public override (ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionPreSelection(GraphQLFieldType fieldType, ExpressionResult baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            var selectParam = Expression.Parameter(nodeExpressionType);
            var idxParam = Expression.Parameter(typeof(int));
            List<MemberBinding> bindings = new();
            // only add the fields they select - avoid redundant GetCursor call
            if (selectionExpressions.Values.Any(c => c.Field.Name == "node"))
                bindings.Add(Expression.Bind(newEdgeType.GetProperty("Node"), selectParam));
            bool hasCursorField = selectionExpressions.Values.Any(c => c.Field.Name == "cursor");
            if (hasCursorField)
                bindings.Add(Expression.Bind(newEdgeType.GetProperty("Cursor"), Expression.Call(typeof(ConnectionHelper), "GetCursor", null, ArgExpression, idxParam)));

            var edgesExp = (ExpressionResult)Expression.Call(typeof(Enumerable), "Select", new Type[] { nodeExpressionType, newEdgeType },
                Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Select", new Type[] { listType, nodeExpressionType },
                    connectionPagingExtension.EdgeExpression,
                    // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
                    Expression.Lambda(nodeExpression, firstSelectParam)
                ),
                Expression.Lambda(
                    Expression.MemberInit(Expression.New(newEdgeType),
                        bindings
                    ),
                    selectParam,
                    idxParam
                )
            );
            edgesExp.AddServices(baseExpression.Services);
            edgesExp.AddConstantParameters(baseExpression.ConstantParameters);

            // we have an extension handling things for the Node field. For Cursor we need to fix the parameter
            if (hasCursorField)
            {
                var exp = selectionExpressions.First(i => i.Value.Field.Name == "cursor");
                exp.Value.Expression.Expression = Expression.PropertyOrField(newEdgeParam, "Cursor");
            }

            return (edgesExp, selectionExpressions, newEdgeParam);
        }

        internal void SetNodeExpression(Expression nodeExpression, Type nodeExpressionType, ParameterExpression newEdgeParam)
        {
            this.nodeExpression = nodeExpression;
            this.nodeExpressionType = nodeExpressionType;
            this.newEdgeParam = newEdgeParam;
            this.newEdgeType = newEdgeParam.Type;
        }
    }
}