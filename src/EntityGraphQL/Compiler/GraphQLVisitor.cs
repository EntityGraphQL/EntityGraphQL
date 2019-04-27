using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Compiler;
using EntityGraphQL.Extensions;
using EntityGraphQL.Grammer;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL;
using EntityGraphQL.LinqQuery;
using EntityGraphQL.Compiler.Util;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Visits nodes of a GraphQL request to build a representation of the query against the context objects via LINQ methods.
    /// </summary>
    /// <typeparam name="IGraphQLNode"></typeparam>
    internal class GraphQLVisitor : EntityGraphQLBaseVisitor<IGraphQLNode>
    {
        private ISchemaProvider schemaProvider;
        private IMethodProvider methodProvider;
        private readonly QueryVariables variables;

        // This is really just so we know what to use when visiting a field
        private Expression selectContext;
        private BaseIdentityFinder baseIdentityFinder = new BaseIdentityFinder();
        /// <summary>
        /// As we parse the request fragments are added to this
        /// </summary>
        private List<GraphQLFragment> fragments = new List<GraphQLFragment>();
        /// <summary>
        /// Each request has 1 main "action" which is a query or a mutation
        /// </summary>
        private List<IGraphQLNode> rootQueries = new List<IGraphQLNode>();

        public GraphQLVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables)
        {
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.variables = variables;
        }

        public override IGraphQLNode VisitField(EntityGraphQLParser.FieldContext context)
        {
            var name = baseIdentityFinder.Visit(context);
            var result = EqlCompiler.CompileWith(context.GetText(), selectContext, schemaProvider, methodProvider, variables);
            var actualName = schemaProvider.GetActualFieldName(schemaProvider.GetSchemaTypeNameForRealType(selectContext.Type), name);
            var node = new GraphQLNode(actualName, result, null);
            return node;
        }
        public override IGraphQLNode VisitAliasExp(EntityGraphQLParser.AliasExpContext context)
        {
            var name = context.alias.name.GetText();
            var query = context.entity.GetText();
            if (selectContext == null)
            {
                // top level are queries on the context
                var exp = EqlCompiler.Compile(query, schemaProvider, methodProvider, variables);
                var node = new GraphQLNode(name, exp, null);
                return node;
            }
            else
            {
                var result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, methodProvider, variables);
                var node = new GraphQLNode(name, result, null);
                return node;
            }
        }

        /// <summary>
        /// We compile each entityQuery with EqlCompiler and build a Select call from the fields
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLNode VisitEntityQuery(EntityGraphQLParser.EntityQueryContext context)
        {
            string name;
            string query;
            if (context.alias != null)
            {
                name = context.alias.name.GetText();
                query = context.entity.GetText();
            }
            else
            {
                query = context.entity.GetText();
                name = query;
                if (name.IndexOf(".") > -1)
                    name = name.Substring(0, name.IndexOf("."));
                if (name.IndexOf("(") > -1)
                    name = name.Substring(0, name.IndexOf("("));
            }

            try
            {
                CompiledQueryResult result = null;
                if (selectContext == null)
                {
                    // top level are queries on the context
                    result = EqlCompiler.Compile(query, schemaProvider, methodProvider, variables);
                }
                else
                {
                    result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, methodProvider, variables);
                }
                var exp = result.LambdaExpression.Body;

                IGraphQLNode graphQLNode = null;
                if (exp.Type.IsEnumerableOrArray())
                {
                    graphQLNode = BuildDynamicSelectOnCollection(result, name, context, true);
                }
                else
                {
                    // Could be a list.First() that we need to turn into a select, or
                    // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                    // Can we turn a list.First() into and list.Select().First()
                    var listExp = Compiler.Util.ExpressionUtil.FindIEnumerable(result.LambdaExpression.Body);
                    if (listExp.Item1 != null)
                    {
                        // yes we can
                        graphQLNode = BuildDynamicSelectOnCollection(new CompiledQueryResult((ExpressionResult)listExp.Item1, result.ContextParams, result.ConstantParameterValues), name, context, true);
                        graphQLNode.NodeExpression = (ExpressionResult)Compiler.Util.ExpressionUtil.CombineExpressions(graphQLNode.NodeExpression, listExp.Item2);
                    }
                    else
                    {
                        graphQLNode = BuildDynamicSelectForObjectGraph(query, name, context, result);
                    }
                }
                // the query result may be a mutation
                if (result.IsMutation)
                {
                    return new GraphQLMutationNode(result, graphQLNode);
                }
                return graphQLNode;
            }
            catch (EntityGraphQLCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerables
        private IGraphQLNode BuildDynamicSelectOnCollection(CompiledQueryResult queryResult, string name, EntityGraphQLParser.EntityQueryContext context, bool isRootSelect)
        {
            var elementType = queryResult.BodyType.GetEnumerableOrArrayType();
            var contextParameter = Expression.Parameter(elementType);

            var exp = queryResult.LambdaExpression.Body;

            var oldContext = selectContext;
            selectContext = contextParameter;
            // visit child fields. Will be field or entityQueries again
            var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

            // Default we select out sub objects/relations. So Select(d => new {Field = d.Field, Relation = new { d.Relation.Field }})
            var selectExpression = (ExpressionResult)ExpressionUtil.SelectDynamic(contextParameter, exp, fieldExpressions, schemaProvider);
            selectContext = oldContext;

            var t = MergeConstantParametersFromFields(queryResult, fieldExpressions, contextParameter);
            var parameters = t.Item1;
            var constantParameterValues = t.Item2;
            var gqlNode = new GraphQLNode(name, new CompiledQueryResult(selectExpression, parameters, constantParameterValues), (ExpressionResult)exp);
            return gqlNode;
        }

        private static Tuple<List<ParameterExpression>, List<object>> MergeConstantParametersFromFields(CompiledQueryResult queryResult, List<IGraphQLNode> fieldExpressions, ParameterExpression parameterExpression)
        {
            var parameters = queryResult.IsMutation ? new List<ParameterExpression> {parameterExpression} : queryResult.LambdaExpression.Parameters.ToList();
            var constantParameterValues = queryResult.ConstantParameterValues.ToList();
            fieldExpressions.ForEach(field =>
            {
                if (field.ConstantParameterValues != null && field.ConstantParameterValues.Any())
                {
                    parameters.AddRange(field.Parameters);
                    constantParameterValues.AddRange(field.ConstantParameterValues);
                }
            });
            return Tuple.Create(parameters, constantParameterValues);
        }

        /// Given a syntax of someField { fields, to, selection, from, object }
        /// it will build the correct select statement
        private IGraphQLNode BuildDynamicSelectForObjectGraph(string query, string name, EntityGraphQLParser.EntityQueryContext context, CompiledQueryResult rootField)
        {
            var selectWasNull = false;
            if (selectContext == null)
            {
                selectContext = Expression.Parameter(schemaProvider.ContextType);
                selectWasNull = true;
            }

            if (schemaProvider.TypeHasField(selectContext.Type.Name, name, new string[0]))
            {
                name = schemaProvider.GetActualFieldName(selectContext.Type.Name, name);
            }

            try
            {
                Expression exp = rootField.LambdaExpression.Body;

                var oldContext = selectContext;
                var rootFieldParam = Expression.Parameter(exp.Type);
                selectContext = rootField.IsMutation ? rootFieldParam : exp;
                // visit child fields. Will be field or entityQueries again
                var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

                var newExp = ExpressionUtil.CreateNewExpression(selectContext, fieldExpressions, schemaProvider);
                var anonType = newExp.Type;
                // make a null check from this new expression
                if (!rootField.IsMutation)
                {
                    newExp = Expression.Condition(Expression.MakeBinary(ExpressionType.Equal, selectContext, Expression.Constant(null)), Expression.Constant(null, anonType), newExp, anonType);
                }
                selectContext = oldContext;

                var t = MergeConstantParametersFromFields(rootField, fieldExpressions, rootFieldParam);
                var parameters = t.Item1;
                var constantParameterValues = t.Item2;

                var graphQLNode = new GraphQLNode(name, new CompiledQueryResult((ExpressionResult)newExp, parameters, constantParameterValues), (ExpressionResult)exp);
                if (selectWasNull)
                {
                    selectContext = null;
                }
                return graphQLNode;
            }
            catch (EntityGraphQLCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// <summary>
        /// This is out TOP level GQL result
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLNode VisitGraphQL(EntityGraphQLParser.GraphQLContext context)
        {
            foreach (var c in context.children)
            {
                Visit(c);
            }
            return new GraphQLResultNode(rootQueries, fragments);
        }

        /// <summary>
        /// This is one of our top level node.
        /// query MyQuery {
        ///   entityQuery { fields [, field] },
        ///   entityQuery { fields [, field] },
        ///   ...
        /// }
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLNode VisitDataQuery(EntityGraphQLParser.DataQueryContext context)
        {
            var operationName = GetOperationName(context.operationName());
            var query = new GraphQLNode(operationName.Name, null, null, null, null);
            // Just visit each child node. All top level will be entityQueries
            foreach (var c in context.gqlBody().children)
            {
                var n = Visit(c);
                if (n != null)
                    query.Fields.Add(n);
            }
            rootQueries.Add(query);
            return query;
        }
        /// <summary>
        /// This is one of our top level node.
        /// mutation MyMutation {...}
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLNode VisitMutationQuery(EntityGraphQLParser.MutationQueryContext context)
        {
            var operationName = GetOperationName(context.operationName());
            var mutation = new GraphQLNode(operationName.Name, null, null, null, null);
            foreach (var c in context.gqlBody().children)
            {
                var n = Visit(c);
                if (n != null)
                {
                    mutation.Fields.Add(n);
                }
            }
            rootQueries.Add(mutation);
            return mutation;
        }

        public GraphQLOperation GetOperationName(EntityGraphQLParser.OperationNameContext context)
        {
            if (context == null)
            {
                return new GraphQLOperation();
            }
            var visitor = new OperationVisitor(variables);
            var op = visitor.Visit(context);

            return op;
        }

        public override IGraphQLNode VisitGqlFragment(EntityGraphQLParser.GqlFragmentContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var typeName = context.fragmentType.GetText();
            selectContext = Expression.Parameter(schemaProvider.Type(typeName).ContextType);
            foreach (var item in context.fields.children)
            {
                var fields2 = Visit(item);
            }
            var fields = Visit(context.fields);
            selectContext = null;
            fragments.Add(new GraphQLFragment(context.fragmentName.GetText(), typeName));
            return null;
        }

        public override IGraphQLNode VisitFragmentSelect(EntityGraphQLParser.FragmentSelectContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var name = context.name.GetText();
            return null;
        }
    }
}
