using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Grammer;
using EntityGraphQL.Schema;
using System.Collections.Generic;
using EntityGraphQL.LinqQuery;
using EntityGraphQL.Compiler.Util;
using System.Security.Claims;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Visits nodes of a GraphQL request to build a representation of the query against the context objects via LINQ methods.
    /// </summary>
    /// <typeparam name="IGraphQLBaseNode"></typeparam>
    internal class GraphQLVisitor : EntityGraphQLBaseVisitor<IGraphQLBaseNode>
    {
        private readonly ClaimsIdentity claims;
        private readonly ISchemaProvider schemaProvider;
        private readonly IMethodProvider methodProvider;
        private readonly QueryVariables variables;

        // This is really just so we know what to use when visiting a field
        private Expression selectContext;
        private readonly BaseIdentityFinder baseIdentityFinder = new BaseIdentityFinder();
        /// <summary>
        /// As we parse the request fragments are added to this
        /// </summary>
        private readonly List<GraphQLFragment> fragments = new List<GraphQLFragment>();
        /// <summary>
        /// Each request has 1 main "action" which is a query or a mutation
        /// </summary>
        private readonly List<IGraphQLNode> rootQueries = new List<IGraphQLNode>();

        public GraphQLVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, QueryVariables variables, ClaimsIdentity claims)
        {
            this.claims = claims;
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.variables = variables;
        }

        public override IGraphQLBaseNode VisitField(EntityGraphQLParser.FieldContext context)
        {
            var name = baseIdentityFinder.Visit(context);
            var result = EqlCompiler.CompileWith(context.GetText(), selectContext, schemaProvider, claims, methodProvider, variables);
            var actualName = schemaProvider.GetActualFieldName(schemaProvider.GetSchemaTypeNameForClrType(selectContext.Type), name, claims);
            var node = new GraphQLNode(schemaProvider, fragments, actualName, result, null);
            return node;
        }
        public override IGraphQLBaseNode VisitAliasExp(EntityGraphQLParser.AliasExpContext context)
        {
            var name = context.alias.name.GetText();
            var query = context.entity.GetText();
            if (selectContext == null)
            {
                // top level are queries on the context
                var exp = EqlCompiler.Compile(query, schemaProvider, claims, methodProvider, variables);
                var node = new GraphQLNode(schemaProvider, fragments, name, exp, null);
                return node;
            }
            else
            {
                var result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, claims, methodProvider, variables);
                var node = new GraphQLNode(schemaProvider, fragments, name, result, null);
                return node;
            }
        }

        /// <summary>
        /// We compile each entityQuery with EqlCompiler and build a Select call from the fields
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public override IGraphQLBaseNode VisitEntityQuery(EntityGraphQLParser.EntityQueryContext context)
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
                    result = EqlCompiler.Compile(query, schemaProvider, claims, methodProvider, variables);
                }
                else
                {
                    result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, claims, methodProvider, variables);
                }
                var exp = result.ExpressionResult;

                IGraphQLNode graphQLNode = null;
                if (exp.Type.IsEnumerableOrArray())
                {
                    graphQLNode = BuildDynamicSelectOnCollection(result, name, context);
                }
                else
                {
                    // Could be a list.First() that we need to turn into a select, or
                    // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                    // Can we turn a list.First() into and list.Select().First()
                    var listExp = ExpressionUtil.FindIEnumerable(result.ExpressionResult);
                    if (listExp.Item1 != null)
                    {
                        // yes we can
                        // rebuild the ExpressionResult so we keep any ConstantParameters
                        var item1 = (ExpressionResult)listExp.Item1;
                        item1.AddConstantParameters(result.ExpressionResult.ConstantParameters);
                        graphQLNode = BuildDynamicSelectOnCollection(new CompiledQueryResult(item1, result.ContextParams), name, context);
                        graphQLNode.SetNodeExpression((ExpressionResult)ExpressionUtil.CombineExpressions(graphQLNode.GetNodeExpression(), listExp.Item2));
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
        private IGraphQLNode BuildDynamicSelectOnCollection(CompiledQueryResult queryResult, string name, EntityGraphQLParser.EntityQueryContext context)
        {
            var elementType = queryResult.BodyType.GetEnumerableOrArrayType();
            var contextParameter = Expression.Parameter(elementType, $"param_{elementType}");

            var exp = queryResult.ExpressionResult;

            var oldContext = selectContext;
            selectContext = contextParameter;
            // visit child fields. Will be field or entityQueries again
            var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

            var gqlNode = new GraphQLNode(schemaProvider, fragments, name, null, exp, queryResult.ContextParams, fieldExpressions, contextParameter);

            selectContext = oldContext;

            return gqlNode;
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

            if (schemaProvider.TypeHasField(selectContext.Type.Name, name, new string[0], claims))
            {
                name = schemaProvider.GetActualFieldName(selectContext.Type.Name, name, claims);
            }

            try
            {
                var exp = (Expression)rootField.ExpressionResult;

                var oldContext = selectContext;
                var rootFieldParam = Expression.Parameter(exp.Type);
                selectContext = rootField.IsMutation ? rootFieldParam : exp;
                // visit child fields. Will be field or entityQueries again
                var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

                var graphQLNode = new GraphQLNode(schemaProvider, fragments, name, null, (ExpressionResult)selectContext, (rootField.IsMutation ? new ParameterExpression[] { rootFieldParam } : rootField.ContextParams.ToArray()), fieldExpressions, null);
                if (rootField != null && rootField.ConstantParameters != null)
                {
                    graphQLNode.AddConstantParameters(rootField.ConstantParameters);
                }

                selectContext = oldContext;

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
        public override IGraphQLBaseNode VisitGraphQL(EntityGraphQLParser.GraphQLContext context)
        {
            foreach (var c in context.children)
            {
                Visit(c);
            }
            return new GraphQLResultNode(rootQueries);
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
        public override IGraphQLBaseNode VisitDataQuery(EntityGraphQLParser.DataQueryContext context)
        {
            var operation = GetOperation(context.operationName());
            foreach (var item in operation.Arguments.Where(a => a.DefaultValue != null))
            {
                variables[item.ArgName] = Expression.Lambda(item.DefaultValue.Expression).Compile().DynamicInvoke();
            }
            var query = new GraphQLNode(schemaProvider, fragments, operation.Name, null, null, null, null, null);
            // Just visit each child node. All top level will be entityQueries
            foreach (var c in context.gqlBody().children)
            {
                var n = Visit(c);
                if (n != null)
                    query.AddField((IGraphQLNode)n);
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
        public override IGraphQLBaseNode VisitMutationQuery(EntityGraphQLParser.MutationQueryContext context)
        {
            var operation = GetOperation(context.operationName());
            foreach (var item in operation.Arguments.Where(a => a.DefaultValue != null))
            {
                variables[item.ArgName] = Expression.Lambda(item.DefaultValue.Expression).Compile().DynamicInvoke();
            }
            var mutation = new GraphQLNode(schemaProvider, fragments, operation.Name, null, null, null, null, null);
            foreach (var c in context.gqlBody().children)
            {
                var n = Visit(c);
                if (n != null)
                {
                    mutation.AddField((IGraphQLNode)n);
                }
            }
            rootQueries.Add(mutation);
            return mutation;
        }

        public GraphQLOperation GetOperation(EntityGraphQLParser.OperationNameContext context)
        {
            if (context == null)
            {
                return new GraphQLOperation();
            }
            var visitor = new OperationVisitor(variables, schemaProvider, claims);
            var op = visitor.Visit(context);

            return op;
        }

        public override IGraphQLBaseNode VisitGqlFragment(EntityGraphQLParser.GqlFragmentContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var typeName = context.fragmentType.GetText();
            selectContext = Expression.Parameter(schemaProvider.Type(typeName).ContextType, $"fragment_{typeName}");
            var fields = new List<IGraphQLBaseNode>();
            foreach (var item in context.fields.children)
            {
                var f = Visit(item);
                if (f != null) // white space etc
                    fields.Add(f);
            }
            fragments.Add(new GraphQLFragment(context.fragmentName.GetText(), typeName, fields, (ParameterExpression)selectContext));
            selectContext = null;
            return null;
        }

        public override IGraphQLBaseNode VisitFragmentSelect(EntityGraphQLParser.FragmentSelectContext context)
        {
            // top level syntax part. Add to the fragrments and return null
            var name = context.name.GetText();
            return new GraphQLFragmentSelect(name);
        }
    }
}
