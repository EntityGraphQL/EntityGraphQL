using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using EntityQueryLanguage.Compiler;
using EntityQueryLanguage.GraphQL.Util;
using EntityQueryLanguage.Extensions;
using EntityQueryLanguage.Grammer;
using EntityQueryLanguage.Schema;

namespace EntityQueryLanguage.GraphQL.Parsing
{
    public class GraphQLCompiler
    {
        private ISchemaProvider _schemaProvider;
        private IMethodProvider _methodProvider;
        private IRelationHandler _relationHandler;
        public GraphQLCompiler(ISchemaProvider schemaProvider, IMethodProvider methodProvider, IRelationHandler relationHandler = null)
        {
            _schemaProvider = schemaProvider;
            _methodProvider = methodProvider;
            _relationHandler = relationHandler;
        }

        /// Parses a GraphQL-like query syntax into a tree respresenting the requested object graph. E.g.
        /// {
        ///   entity/query {
        ///     field1,
        ///     field2,
        ///     relation { field }
        ///   },
        ///   ...
        /// }
        ///
        /// The returned DataQueryNode is a root node, it's Fields are the top level data queries
        public GraphQLNode Compile(string query)
        {
            // Setup our Antlr parser
            var stream = new AntlrInputStream(query);
            var lexer = new EqlGrammerLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EqlGrammerParser(tokens);
            parser.BuildParseTree = true;
            parser.ErrorHandler = new BailErrorStrategy();
            try
            {
                var tree = parser.dataQuery();
                var visitor = new DataApiVisitor(_schemaProvider, _methodProvider, _relationHandler);
                // visit each node. it will return a linq expression for each entity requested
                var node = visitor.Visit(tree);
                return node;
            }
            catch (ParseCanceledException pce)
            {
                if (pce.InnerException != null)
                {
                    if (pce.InnerException is NoViableAltException)
                    {
                        var nve = (NoViableAltException)pce.InnerException;
                        throw new EqlCompilerException($"Error: line {nve.OffendingToken.Line}:{nve.OffendingToken.Column} no viable alternative at input '{nve.OffendingToken.Text}'");
                    }
                    else if (pce.InnerException is InputMismatchException)
                    {
                        var ime = (InputMismatchException)pce.InnerException;
                        var expecting = string.Join(", ", ime.GetExpectedTokens());
                        throw new EqlCompilerException($"Error: line {ime.OffendingToken.Line}:{ime.OffendingToken.Column} extraneous input '{ime.OffendingToken.Text}' expecting {expecting}");
                    }
                    System.Console.WriteLine(pce.InnerException.GetType());
                    throw new EqlCompilerException(pce.InnerException.Message);
                }
                throw new EqlCompilerException(pce.Message);
            }
        }

        /// Visits nodes of a DataQuery to build a list of linq expressions for each requested entity.
        /// We use EqlCompiler to compile the query and then build a Select() call for each field
        private class DataApiVisitor : EqlGrammerBaseVisitor<GraphQLNode>
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

                var node = new GraphQLNode(actualName, fieldExp, null, null);
                return node;
            }
            public override GraphQLNode VisitAliasExp(EqlGrammerParser.AliasExpContext context)
            {
                var name = context.alias.name.GetText();
                var query = context.entity.GetText();
                Expression result;
                if (_selectContext == null)
                {
                    // top level are queries on the context
                    var exp = EqlCompiler.Compile(query, _schemaProvider, _methodProvider).Expression;
                    var node = new GraphQLNode(name, exp.Body, exp.Parameters.First(), null);
                    return node;
                }
                else
                {
                    result = EqlCompiler.CompileWith(query, _selectContext, _schemaProvider, _methodProvider).Expression.Body;
                    var node = new GraphQLNode(name, result, null, null);
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
                }

                try
                {
                    if (_selectContext == null)
                    {
                        // top level are queries on the context
                        var result = EqlCompiler.Compile(query, _schemaProvider, _methodProvider);
                        var exp = result.Expression.Body;

                        if (exp.Type.IsEnumerable())
                        {
                            exp = BuildDynamicSelectOnCollection(exp, name, context, true);
                        }
                        var topLevelSelect = new GraphQLNode(name, exp, result.Expression.Parameters.Any() ? result.Expression.Parameters.First() : null, exp);
                        return topLevelSelect;
                    }
                    // other levels are object selection. e.g. from the top level people query I am selecting all their children { field1, etc. }
                    return BuildDynamicSelectForObjectGraph(query, name, context);
                }
                catch (EqlCompilerException ex)
                {
                    //return DataApiNode.MakeError(name, $"Error compiling field or query '{query}'. {ex.Message}");
                    throw DataApiException.MakeFieldCompileError(query, ex.Message);
                }
            }

            /// Given a syntax of someCollection { fields, to, selection, from, object }
            /// it will build a select assuming 'someCollection' is an IEnumerables
            private ExpressionResult BuildDynamicSelectOnCollection(Expression exp, string name, EqlGrammerParser.EntityQueryContext context, bool isRootSelect)
            {
                var elementType = exp.Type.GetEnumerableType();
                var contextParameter = Expression.Parameter(elementType);

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
                return selectExpression;
            }

            /// Given a syntax of someField { fields, to, selection, from, object }
            /// it will figure out if 'someField' is an IEnumerable or an istance of the object (not a collection) and build the correct select statement
            private GraphQLNode BuildDynamicSelectForObjectGraph(string query, string name, EqlGrammerParser.EntityQueryContext context)
            {
                if (!_schemaProvider.TypeHasField(_selectContext.Type.Name, name))
                    throw new EqlCompilerException($"Type {_selectContext.Type} does not have field or property {name}");
                name = _schemaProvider.GetActualFieldName(_selectContext.Type.Name, name);

                // Don't really like any of this, but...
                try
                {
                    var result = EqlCompiler.CompileWith(query, _selectContext, _schemaProvider, _methodProvider);
                    Expression exp = result.Expression.Body;
                    if (exp.Type.IsEnumerable())
                    {
                        var r = BuildDynamicSelectOnCollection(exp, name, context, false);
                        var selectOnCollection = new GraphQLNode(name, r, result.Expression.Parameters.Any() ? result.Expression.Parameters.First() : null, exp);
                        return selectOnCollection;
                    }

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
                    return new GraphQLNode(_schemaProvider.GetActualFieldName(_selectContext.Type.Name, name), newExp, result.Expression.Parameters.Any() ? result.Expression.Parameters.First() : null, exp);
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
                var root = new GraphQLNode("root", null, null, null);
                // Just visit each child node. All top level will be entityQueries
                var entities = context.children.Select(c => Visit(c)).ToList();
                root.Fields.AddRange(entities.Where(n => n != null));
                return root;
            }
        }
    }

    public class DataApiException : Exception
    {
        public DataApiException(string message) : base(message) { }
        public static DataApiException MakeFieldCompileError(string query, string message)
        {
            return new DataApiException($"Error compiling field or query '{query}'. {message}");
        }
    }
}
