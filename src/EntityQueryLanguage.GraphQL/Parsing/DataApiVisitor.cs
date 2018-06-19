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
    internal class DataApiVisitor : EqlGrammerBaseVisitor<GraphQLNode>
    {
        private ISchemaProvider schemaProvider;
        private IMethodProvider methodProvider;
        private IRelationHandler relationHandler;
        // This is really just so we know what to use when visiting a field
        private Expression selectContext;
        private BaseIdentityFinder baseIdentityFinder = new BaseIdentityFinder();

        public DataApiVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler)
        {
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
            this.relationHandler = relationHandler;
        }

        public override GraphQLNode VisitField(EqlGrammerParser.FieldContext context)
        {
            var name = baseIdentityFinder.Visit(context);
            var result = EqlCompiler.CompileWith(context.GetText(), selectContext, schemaProvider, methodProvider);
            var actualName = schemaProvider.GetActualFieldName(selectContext.Type.Name, name);
            var node = new GraphQLNode(actualName, result, null);
            return node;
        }
        public override GraphQLNode VisitAliasExp(EqlGrammerParser.AliasExpContext context)
        {
            var name = context.alias.name.GetText();
            var query = context.entity.GetText();
            if (selectContext == null)
            {
                // top level are queries on the context
                var exp = EqlCompiler.Compile(query, schemaProvider, methodProvider);
                var node = new GraphQLNode(name, exp, null);
                return node;
            }
            else
            {
                var result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, methodProvider);
                var node = new GraphQLNode(name, result, null);
                return node;
            }
        }

        /// We compile each entityQuery with EqlCompiler and build a Select call from the fields
        public override GraphQLNode VisitEntityQuery(EqlGrammerParser.EntityQueryContext context)
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
                    result = EqlCompiler.Compile(query, schemaProvider, methodProvider);
                }
                else
                {
                    result = EqlCompiler.CompileWith(query, selectContext, schemaProvider, methodProvider);
                }
                var exp = result.Expression.Body;

                if (exp.Type.IsEnumerable())
                {
                    return BuildDynamicSelectOnCollection(result, name, context, true);
                }
                // Could be a list.First() that we need to turn into a select, or
                // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                return BuildDynamicSelectForObjectGraph(query, name, context, result);
            }
            catch (EqlCompilerException ex)
            {
                throw SchemaException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerables
        private GraphQLNode BuildDynamicSelectOnCollection(QueryResult queryResult, string name, EqlGrammerParser.EntityQueryContext context, bool isRootSelect)
        {
            var elementType = queryResult.BodyType.GetEnumerableType();
            var contextParameter = Expression.Parameter(elementType);

            var exp = queryResult.Expression.Body;

            var oldContext = selectContext;
            selectContext = contextParameter;
            // visit child fields. Will be field or entityQueries again
            var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
            var relations = fieldExpressions.Where(f => f.Expression.NodeType == ExpressionType.MemberInit || f.Expression.NodeType == ExpressionType.Call).Select(r => r.RelationExpression).Where(n => n != null).ToList();
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

            var t = MergeConstantParametersFromFields(queryResult, fieldExpressions);
            var parameters = t.Item1;
            var constantParameterValues = t.Item2;
            var lambda = Expression.Lambda(selectExpression, parameters);
            var gqlNode = new GraphQLNode(name, new QueryResult(lambda, constantParameterValues), exp);
            return gqlNode;
        }

        private static Tuple<List<ParameterExpression>, List<object>> MergeConstantParametersFromFields(QueryResult queryResult, List<GraphQLNode> fieldExpressions)
        {
            var parameters = queryResult.Expression.Parameters.ToList();
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
        /// it will figure out if 'someField' is an IEnumerable or an istance of the object (not a collection) and build the correct select statement
        private GraphQLNode BuildDynamicSelectForObjectGraph(string query, string name, EqlGrammerParser.EntityQueryContext context, QueryResult rootField)
        {
            if (selectContext == null)
                selectContext = Expression.Parameter(schemaProvider.ContextType);

            if (!schemaProvider.TypeHasField(selectContext.Type.Name, name))
                throw new EqlCompilerException($"Type {selectContext.Type} does not have field or property {name}");
            name = schemaProvider.GetActualFieldName(selectContext.Type.Name, name);

            try
            {
                Expression exp = rootField.Expression.Body;

                var oldContext = selectContext;
                selectContext = exp;
                // visit child fields. Will be field or entityQueries again
                var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
                var relationsExps = fieldExpressions.Where(f => f.Expression.NodeType == ExpressionType.MemberInit || f.Expression.NodeType == ExpressionType.Call).Where(n => n != null).ToList();
                if (relationHandler != null && relationsExps.Any())
                {
                    var parameterExpression = Expression.Parameter(selectContext.Type);
                    var relations = relationsExps.Select(r => (Expression)Expression.PropertyOrField(parameterExpression, r.Name)).ToList();
                    exp = relationHandler.BuildNodeForSelect(relations, parameterExpression, exp);
                }

                var newExp = DataApiExpressionUtil.CreateNewExpression(selectContext, fieldExpressions, schemaProvider);
                selectContext = oldContext;

                var t = MergeConstantParametersFromFields(rootField, fieldExpressions);
                var parameters = t.Item1;
                var constantParameterValues = t.Item2;
                var lambda = Expression.Lambda(newExp, parameters);
                return new GraphQLNode(schemaProvider.GetActualFieldName(selectContext.Type.Name, name), new QueryResult(lambda, constantParameterValues), exp);
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
        public override GraphQLNode VisitDataQuery(EqlGrammerParser.DataQueryContext context)
        {
            var root = new GraphQLNode("root", null, null, null, null);
            // Just visit each child node. All top level will be entityQueries
            var entities = context.children.Select(c => Visit(c)).ToList();
            root.Fields.AddRange(entities.Where(n => n != null));
            return root;
        }
    }
}
