using EntityGraphQL.Schema;
using HotChocolate.Language;
using System.Security.Claims;

namespace EntityGraphQL.Compiler
{
    /// <summary>
    /// Comiles a Graph QL query document string into an AST for processing.
    /// </summary>
    public class GraphQLCompiler
    {
        private readonly ISchemaProvider schemaProvider;

        public GraphQLCompiler(ISchemaProvider schemaProvider)
        {
            this.schemaProvider = schemaProvider;
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
        public GraphQLDocument Compile(string query, QueryVariables? variables = null, IGqlAuthorizationService? authService = null, ClaimsPrincipal? user = null)
        {
            if (variables == null)
            {
                variables = new QueryVariables();
            }
            return Compile(new QueryRequest { Query = query, Variables = variables }, new QueryRequestContext(authService, user));
        }
        public GraphQLDocument Compile(QueryRequest query, IGqlAuthorizationService? authService = null, ClaimsPrincipal? user = null)
        {
            return Compile(query, new QueryRequestContext(authService, user));
        }
        public GraphQLDocument Compile(string query, QueryRequestContext context)
        {
            return Compile(new QueryRequest { Query = query }, context);
        }
        public GraphQLDocument Compile(QueryRequest query, QueryRequestContext context)
        {
            if (query.Query == null)
                throw new EntityGraphQLCompilerException($"GraphQL Query can not be null");

            DocumentNode document = Utf8GraphQLParser.Parse(query.Query, ParserOptions.Default);
            var walker = new EntityGraphQLQueryWalker(schemaProvider, query.Variables, context);
            walker.Visit(document, null);
            if (walker.Document == null)
                throw new EntityGraphQLCompilerException($"Error compiling query: {query.Query}");
            return walker.Document;
        }
    }
}