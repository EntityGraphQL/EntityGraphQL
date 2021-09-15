using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Extensions;

namespace EntityGraphQL.Schema.FieldExtensions
{
    internal class ConnectionEdgeExtension : BaseFieldExtension
    {
        private Expression nodeExpression;
        private Type nodeExpressionType;
        private ParameterExpression newEdgeParam;
        private Type newEdgeType;

        internal ParameterExpression ArgExpression { get; set; }
        public ParameterExpression ArgumentParam { get; internal set; }
        public MethodCallExpression EdgesExpression { get; internal set; }

        private Type listType;
        private ParameterExpression firstSelectParam = null;
        private readonly bool isQueryable;
        private readonly List<IFieldExtension> extensions;
        private ConnectionEdgeNodeExtension nodeFieldExtension;
        private bool isFirstPass = true;

        public ConnectionEdgeExtension(Type listType, bool isQueryable, List<IFieldExtension> extensions)
        {
            this.listType = listType;
            this.isQueryable = isQueryable;
            this.extensions = extensions;
        }

        public override void Configure(ISchemaProvider schema, Field field)
        {
            // We use this extension to "steal" the node selection
            nodeFieldExtension = new ConnectionEdgeNodeExtension(this, null);
            field.ReturnType.SchemaType.GetField("node", null).AddExtension(nodeFieldExtension);
        }

        public override Expression GetExpression(Field field, Expression expression, ParameterExpression argExpression, dynamic arguments, Expression context, ParameterReplacer parameterReplacer)
        {
            firstSelectParam = null;

            return expression;
        }

        public override Expression ProcessExpressionPreSelection(GraphQLFieldType fieldType, Expression baseExpression, ParameterReplacer parameterReplacer)
        {
            listType = baseExpression.Type.GetEnumerableOrArrayType();
            // second pass means we came through without service fields and now with
            isFirstPass = firstSelectParam == null;
            if (!isFirstPass)
            {
                // expression without services has executed and we have a resolve context we're working on
                firstSelectParam = Expression.Parameter(listType.GetField("node").FieldType);
            }
            else
                firstSelectParam = Expression.Parameter(listType);
            nodeFieldExtension.SelectParam = firstSelectParam;
            // baseExpression = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType },
            //     Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType },
            //         baseExpression,
            //         Expression.Call(typeof(ConnectionHelper), "GetSkipNumber", null, ArgumentParam)
            //     ),
            //     Expression.Call(typeof(ConnectionHelper), "GetTakeNumber", null, ArgumentParam)
            // );

            // TODO other previous extensions?

            return baseExpression;
        }
        public override (Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam) ProcessExpressionSelection(GraphQLFieldType fieldType, Expression baseExpression, Dictionary<string, CompiledField> selectionExpressions, ParameterExpression selectContextParam, ParameterReplacer parameterReplacer)
        {
            var selectParam = Expression.Parameter(nodeExpressionType);
            var idxParam = Expression.Parameter(typeof(int));
            List<MemberBinding> bindings = new();
            // only add the fields they select - avoid redundant GetCursor call
            bool hasNodeField = selectionExpressions.Values.Any(c => c.Field.Name == "node");
            bool hasCursorField = selectionExpressions.Values.Any(c => c.Field.Name == "cursor");
            Expression edgesExp;

            if (isFirstPass)
            {
                if (hasNodeField)
                    bindings.Add(Expression.Bind(newEdgeType.GetProperty("Node"), selectParam));
                if (hasCursorField)
                    bindings.Add(Expression.Bind(newEdgeType.GetProperty("Cursor"), Expression.Call(typeof(ConnectionHelper), "GetCursor", null, ArgExpression, idxParam)));

                var edgeExpression = Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Take", new Type[] { listType },
                    Expression.Call(isQueryable ? typeof(QueryableExtensions) : typeof(EnumerableExtensions), "Skip", new Type[] { listType },
                        baseExpression,
                        Expression.Call(typeof(ConnectionHelper), "GetSkipNumber", null, ArgumentParam)
                    ),
                    Expression.Call(typeof(ConnectionHelper), "GetTakeNumber", null, ArgumentParam)
                );

                // first pass we want to insert the selection
                edgeExpression = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Select", new Type[] { listType, nodeExpressionType },
                    edgeExpression,
                    // we have the node selection from ConnectionEdgeNodeExtension we can insert into here for a nice EF compatible query
                    Expression.Lambda(nodeExpression, firstSelectParam)
                );

                edgesExp = Expression.Call(typeof(Enumerable), "Select", new Type[] { nodeExpressionType, newEdgeType },
                    edgeExpression,
                    Expression.Lambda(
                        Expression.MemberInit(Expression.New(newEdgeType),
                            bindings
                        ),
                        selectParam,
                        idxParam
                    )
                );

                // we have an extension handling things for the Node field. For Cursor we need to fix the parameter
                if (hasCursorField)
                {
                    var exp = selectionExpressions.First(i => i.Value.Field.Name == "cursor");
                    exp.Value.Expression = Expression.PropertyOrField(newEdgeParam, "Cursor");
                }
                return (edgesExp, selectionExpressions, newEdgeParam);
            }
            else
            {
                edgesExp = baseExpression;
                return (baseExpression, selectionExpressions, selectContextParam);
                // var finalParam = Expression.Parameter(newEdgeType);
                // if (hasNodeField)
                //     bindings.Add(Expression.Bind(newEdgeType.GetProperty("Node"), selectParam));
                // if (hasCursorField)
                //     bindings.Add(Expression.Bind(newEdgeType.GetProperty("Cursor"), selectParam));

                // edgesExp = Expression.Call(isQueryable ? typeof(Queryable) : typeof(Enumerable), "Select", new Type[] { listType, newEdgeType },
                //     baseExpression,
                //     Expression.Lambda(
                //         Expression.MemberInit(Expression.New(newEdgeType),
                //             bindings
                //         ),
                //         finalParam
                //     )
                // );
            }

            // return (edgesExp, selectionExpressions, newEdgeParam);
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