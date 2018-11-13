using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using EntityGraphQL.Compiler;
using EntityGraphQL.Grammer;
using EntityGraphQL.LinqQuery;
using EntityGraphQL.Schema;

namespace EntityGraphQL.Compiler
{
    public class GraphQLCompiler
    {
        private ISchemaProvider _schemaProvider;
        private IMethodProvider _methodProvider;
        public GraphQLCompiler(ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            _schemaProvider = schemaProvider;
            _methodProvider = methodProvider;
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
        public IGraphQLNode Compile(string query, QueryVariables variables = null)
        {
            if (variables == null)
            {
                variables = new QueryVariables();
            }
            return Compile(new QueryRequest {Query = query, Variables = variables});
        }
        public IGraphQLNode Compile(QueryRequest request)
        {
            // Setup our Antlr parser
            var stream = new AntlrInputStream(request.Query);
            var lexer = new EntityGraphQLLexer(stream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new EntityGraphQLParser(tokens);
            parser.BuildParseTree = true;
            parser.ErrorHandler = new BailErrorStrategy();
            try
            {
                var tree = parser.graphQL();
                var visitor = new GraphQLVisitor(_schemaProvider, _methodProvider, request.Variables);
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
                        throw new EntityGraphQLCompilerException($"Error: line {nve.OffendingToken.Line}:{nve.OffendingToken.Column} no viable alternative at input '{nve.OffendingToken.Text}'");
                    }
                    else if (pce.InnerException is InputMismatchException)
                    {
                        var ime = (InputMismatchException)pce.InnerException;
                        var expecting = string.Join(", ", ime.GetExpectedTokens());
                        throw new EntityGraphQLCompilerException($"Error: line {ime.OffendingToken.Line}:{ime.OffendingToken.Column} extraneous input '{ime.OffendingToken.Text}' expecting {expecting}");
                    }
                    System.Console.WriteLine(pce.InnerException.GetType());
                    throw new EntityGraphQLCompilerException(pce.InnerException.Message);
                }
                throw new EntityGraphQLCompilerException(pce.Message);
            }
        }
    }

    public class SchemaException : Exception
    {
        public SchemaException(string message) : base(message) { }
        public static SchemaException MakeFieldCompileError(string query, string message)
        {
            return new SchemaException($"Error compiling field or query '{query}'. {message}");
        }
    }
}
