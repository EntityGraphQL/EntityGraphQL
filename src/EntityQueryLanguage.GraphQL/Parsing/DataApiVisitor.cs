using System.Linq;
using System.Linq.Expressions;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.GraphQL.Util;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    /// Visits nodes of a DataQuery to build a list of linq expressions for each requested entity.
    /// We use EqlCompiler to compile the query and then build a Select() call for each field
    internal class DataApiVisitor : EqlGrammerBaseVisitor<GraphQLNode>
    {
        private ISchemaProvider _schemaProvider;
        private IMethodProvider _methodProvider;
        private IRelationHandler _relationHandler;
        // This is really just so we know what to use when visiting a field
        private Expression _selectContext;

        public DataApiVisitor(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler)
        {
            _schemaProvider = schemaProvider;
            _methodProvider = methodProvider;
            _relationHandler = relationHandler;
        }

        public override GraphQLNode VisitField(EqlGrammerParser.FieldContext context)
        {
            var name = context.GetText();
            if (!_schemaProvider.TypeHasField(_selectContext.Type.Name, name))
                throw new EqlCompilerException($"Type {_selectContext.Type} does not have field or property {name}");

            var actualName = _schemaProvider.GetActualFieldName(_selectContext.Type.Name, name);
            var fieldExp = _schemaProvider.GetExpressionForField(_selectContext, _selectContext.Type.Name, name, null);

            var node = new GraphQLNode(actualName, fieldExp, null);
            return node;
        }
        public override GraphQLNode VisitAliasExp(EqlGrammerParser.AliasExpContext context)
        {
            var name = context.alias.name.GetText();
            var query = context.entity.GetText();
            if (_selectContext == null)
            {
                // top level are queries on the context
                var exp = EqlCompiler.Compile(query, _schemaProvider, _methodProvider);
                var node = new GraphQLNode(name, exp, null);
                return node;
            }
            else
            {
                var result = EqlCompiler.CompileWith(query, _selectContext, _schemaProvider, _methodProvider);
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
                if (_selectContext == null)
                {
                    // top level are queries on the context
                    result = EqlCompiler.Compile(query, _schemaProvider, _methodProvider);
                }
                else
                {
                    result = EqlCompiler.CompileWith(query, _selectContext, _schemaProvider, _methodProvider);
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
                throw DataApiException.MakeFieldCompileError(query, ex.Message);
            }
        }

        /// Given a syntax of someCollection { fields, to, selection, from, object }
        /// it will build a select assuming 'someCollection' is an IEnumerables
        private GraphQLNode BuildDynamicSelectOnCollection(QueryResult queryResult, string name, EqlGrammerParser.EntityQueryContext context, bool isRootSelect)
        {
            var elementType = queryResult.BodyType.GetEnumerableType();
            var contextParameter = Expression.Parameter(elementType);

            var exp = queryResult.Expression.Body;

            var oldContext = _selectContext;
            _selectContext = contextParameter;
            // visit child fields. Will be field or entityQueries again
            var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();
            var relations = fieldExpressions.Where(f => f.Expression.NodeType == ExpressionType.MemberInit || f.Expression.NodeType == ExpressionType.Call).Select(r => r.RelationExpression).ToList();
            // process at each relation
            if (_relationHandler != null && relations.Any())
            {
                // Likely the EF handler to build .Include()s
                exp = _relationHandler.BuildNodeForSelect(relations, contextParameter, exp, name, _schemaProvider);
            }

            // we're about to add the .Select() call. May need to do something
            if (_relationHandler != null && isRootSelect)
            {
                exp = _relationHandler.HandleSelectComplete(exp);
            }

            // Default we select out sub objects/relations. So Select(d => new {Field = d.Field, Relation = new { d.Relation.Field }})
            var selectExpression = (ExpressionResult)DataApiExpressionUtil.SelectDynamic(contextParameter, exp, fieldExpressions, _schemaProvider);
            _selectContext = oldContext;

            var lambda = Expression.Lambda(selectExpression, queryResult.Expression.Parameters);
            var gqlNode = new GraphQLNode(name, new QueryResult(lambda, queryResult.ParameterValues), null);
            return gqlNode;
        }

        /// Given a syntax of someField { fields, to, selection, from, object }
        /// it will figure out if 'someField' is an IEnumerable or an istance of the object (not a collection) and build the correct select statement
        private GraphQLNode BuildDynamicSelectForObjectGraph(string query, string name, EqlGrammerParser.EntityQueryContext context, QueryResult rootField)
        {
            if (_selectContext == null)
                _selectContext = Expression.Parameter(_schemaProvider.ContextType);

            if (!_schemaProvider.TypeHasField(_selectContext.Type.Name, name))
                throw new EqlCompilerException($"Type {_selectContext.Type} does not have field or property {name}");
            name = _schemaProvider.GetActualFieldName(_selectContext.Type.Name, name);

            try
            {
                Expression exp = rootField.Expression.Body;

                var oldContext = _selectContext;
                _selectContext = exp;
                // visit child fields. Will be field or entityQueries again
                var fieldExpressions = context.fields.children.Select(c => Visit(c)).Where(n => n != null).ToList();

                var relationsExps = fieldExpressions.Where(f => f.Expression.NodeType == ExpressionType.MemberInit || f.Expression.NodeType == ExpressionType.Call).ToList();
                if (_relationHandler != null && relationsExps.Any())
                {
                    var parameterExpression = Expression.Parameter(_selectContext.Type);
                    var relations = relationsExps.Select(r => (Expression)Expression.PropertyOrField(parameterExpression, r.Name)).ToList();
                    exp = _relationHandler.BuildNodeForSelect(relations, parameterExpression, exp, name, _schemaProvider);
                }

                var newExp = DataApiExpressionUtil.CreateNewExpression(_selectContext, fieldExpressions, _schemaProvider);
                _selectContext = oldContext;

                // we might need to merge const parameters from the context comming in
                var parameters = rootField.Expression.Parameters.ToList();
                var parameterValues = rootField.ParameterValues.ToList();
                var lambda = Expression.Lambda(newExp, parameters);
                return new GraphQLNode(_schemaProvider.GetActualFieldName(_selectContext.Type.Name, name), new QueryResult(lambda, parameterValues), exp);
            }
            catch (EqlCompilerException ex)
            {
                throw DataApiException.MakeFieldCompileError(query, ex.Message);
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
            var root = new GraphQLNode("root", (Expression)null, null);
            // Just visit each child node. All top level will be entityQueries
            var entities = context.children.Select(c => Visit(c)).ToList();
            root.Fields.AddRange(entities.Where(n => n != null));
            return root;
        }
    }
}
