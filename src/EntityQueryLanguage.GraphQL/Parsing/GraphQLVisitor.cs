using System;
using System.Linq;
using System.Linq.Expressions;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.GraphQL.Util;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Schema;
using System.Collections.Generic;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    /// Visits nodes of a DataQuery to build a list of linq expressions for each requested entity.
    /// We use EqlCompiler to compile the query and then build a Select() call for each field
    internal class GraphQLVisitor : EqlGrammerBaseVisitor<IGraphQLNode>
    {
        private ISchemaProvider schemaProvider;
        private IMethodProvider methodProvider;
        private IRelationHandler relationHandler;
        private readonly QueryVariables variables;

        // This is really just so we know what to use when visiting a field
        private Expression selectContext;
        private BaseIdentityFinder baseIdentityFinder = new BaseIdentityFinder();

        public GraphQLVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler, QueryVariables variables)
        {
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.relationHandler = relationHandler;
            this.variables = variables;
        }

        public override IGraphQLNode VisitField(EqlGrammerParser.FieldContext context)
        {
            var name = baseIdentityFinder.Visit(context);
            var result = EqlCompiler.CompileWith(context.GetText(), selectContext, schemaProvider, methodProvider, variables);
            var actualName = schemaProvider.GetActualFieldName(selectContext.Type.Name, name);
            var node = new GraphQLNode(actualName, result, null);
            return node;
        }
        public override IGraphQLNode VisitAliasExp(EqlGrammerParser.AliasExpContext context)
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

        /// We compile each entityQuery with EqlCompiler and build a Select call from the fields
        public override IGraphQLNode VisitEntityQuery(EqlGrammerParser.EntityQueryContext context)
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
                QueryResult result = null;
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
                if (exp.Type.IsEnumerable())
                {
                    graphQLNode = BuildDynamicSelectOnCollection(result, name, context, true);
                }
                else
                {
                    // Could be a list.First() that we need to turn into a select, or
                    // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                    // Can we turn a list.First() into and list.Select().First()
                    var listExp = ExpressionUtil.FindIEnumerable(result.LambdaExpression.Body);
                    if (listExp.Item1 != null)
                    {
                        // yes we can
                        graphQLNode = BuildDynamicSelectOnCollection(new QueryResult((ExpressionResult)listExp.Item1, result.ContextParams, result.ConstantParameterValues), name, context, true);
                        graphQLNode.NodeExpression = (ExpressionResult)ExpressionUtil.CombineExpressions(graphQLNode.NodeExpression, listExp.Item2);
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
            catch (EqlCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerables
        private IGraphQLNode BuildDynamicSelectOnCollection(QueryResult queryResult, string name, EqlGrammerParser.EntityQueryContext context, bool isRootSelect)
        {
            var elementType = queryResult.BodyType.GetEnumerableType();
            var contextParameter = Expression.Parameter(elementType);

            var exp = queryResult.LambdaExpression.Body;

            var oldContext = selectContext;
            selectContext = contextParameter;
            // visit child fields. Will be field or entityQueries again
            var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
            var relations = fieldExpressions.Where(f => f.NodeExpression.NodeType == ExpressionType.MemberInit || f.NodeExpression.NodeType == ExpressionType.Call).Select(r => r.RelationExpression).Where(n => n != null).ToList();
            // process at each relation
            if (relationHandler != null && relations.Any())
            {
                // Likely the EF handler to build .Include()s
                exp = relationHandler.BuildNodeForSelect(relations, contextParameter, exp);
            }
            // we're about to add the .Select() call. May need to do something
            if (relationHandler != null && isRootSelect)
            {
                exp = relationHandler.HandleSelectComplete(exp);
            }

            // Default we select out sub objects/relations. So Select(d => new {Field = d.Field, Relation = new { d.Relation.Field }})
            var selectExpression = (ExpressionResult)DataApiExpressionUtil.SelectDynamic(contextParameter, exp, fieldExpressions, schemaProvider);
            selectContext = oldContext;

            var t = MergeConstantParametersFromFields(queryResult, fieldExpressions, contextParameter);
            var parameters = t.Item1;
            var constantParameterValues = t.Item2;
            var gqlNode = new GraphQLNode(name, new QueryResult(selectExpression, parameters, constantParameterValues), (ExpressionResult)exp);
            return gqlNode;
        }

        private static Tuple<List<ParameterExpression>, List<object>> MergeConstantParametersFromFields(QueryResult queryResult, List<IGraphQLNode> fieldExpressions, ParameterExpression parameterExpression)
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
        private IGraphQLNode BuildDynamicSelectForObjectGraph(string query, string name, EqlGrammerParser.EntityQueryContext context, QueryResult rootField)
        {
            var selectWasNull = false;
            if (selectContext == null)
            {
                selectContext = Expression.Parameter(schemaProvider.ContextType);
                selectWasNull = true;
            }

            if (schemaProvider.TypeHasField(selectContext.Type.Name, name))
                name = schemaProvider.GetActualFieldName(selectContext.Type.Name, name);

            try
            {
                Expression exp = rootField.LambdaExpression.Body;

                var oldContext = selectContext;
                var rootFieldParam = Expression.Parameter(exp.Type);
                selectContext = rootField.IsMutation ? rootFieldParam : exp;
                // visit child fields. Will be field or entityQueries again
                var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
                var relationsExps = fieldExpressions.Where(f => f.NodeExpression.NodeType == ExpressionType.MemberInit || f.NodeExpression.NodeType == ExpressionType.Call).Where(n => n != null).ToList();
                if (relationHandler != null && relationsExps.Any())
                {
                    var parameterExpression = Expression.Parameter(selectContext.Type);
                    var relations = relationsExps.Select(r => (Expression)Expression.PropertyOrField(parameterExpression, r.Name)).ToList();
                    exp = relationHandler.BuildNodeForSelect(relations, parameterExpression, exp);
                }
                // we're about to add the .Select() call. May need to do something
                if (relationHandler != null)
                {
                    exp = relationHandler.HandleSelectComplete(exp);
                }

                var newExp = DataApiExpressionUtil.CreateNewExpression(selectContext, fieldExpressions, schemaProvider);
                var anonType = newExp.Type;
                // make a null check from this new expression
                if (!rootField.IsMutation)
                {
                    newExp = Expression.IfThenElse(Expression.MakeBinary(ExpressionType.Equal, selectContext, Expression.Constant(null)), Expression.Constant(null, anonType), newExp);
                    // cast it as anonType otherwise Conditional type is System.Void
                    newExp = Expression.TypeAs(newExp, anonType);
                }
                selectContext = oldContext;

                var t = MergeConstantParametersFromFields(rootField, fieldExpressions, rootFieldParam);
                var parameters = t.Item1;
                var constantParameterValues = t.Item2;

                var graphQLNode = new GraphQLNode(name, new QueryResult((ExpressionResult)newExp, parameters, constantParameterValues), (ExpressionResult)exp);
                if (selectWasNull)
                {
                    selectContext = null;
                }
                return graphQLNode;
            }
            catch (EqlCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// This is our top level node.
        /// {
        ///   entityQuery { fields [, field] },
        ///   entityQuery { fields [, field] },
        ///   ...
        /// }
        public override IGraphQLNode VisitDataQuery(EqlGrammerParser.DataQueryContext context)
        {
            var root = new GraphQLNode("root", null, null, null, null);
            // Just visit each child node. All top level will be entityQueries
            foreach (var c in context.gqlBody().children)
            {
                var n = Visit(c);
                if (n != null)
                    root.Fields.Add(n);
            }
            return root;
        }

        public override IGraphQLNode VisitMutationQuery(EqlGrammerParser.MutationQueryContext context)
        {
            var root = new GraphQLNode("root", null, null, null, null);

            var operationName = Visit(context.operationName());
            foreach (var c in context.gqlBody().children)
            {
                var mutation = Visit(c);
                if (mutation != null)
                {
                    root.Fields.Add(mutation);
                }
            }
            return root;
        }

    }
}
