using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
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
