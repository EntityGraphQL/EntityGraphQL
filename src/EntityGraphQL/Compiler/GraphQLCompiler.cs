using EntityGraphQL.Schema;
using HotChocolate.Language;
using System.Security.Claims;
using System.Text;

namespace EntityGraphQL.Compiler
{
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
        public GraphQLDocument Compile(string query, QueryVariables variables = null, IGqlAuthorizationService authService = null, ClaimsPrincipal user = null)
        {
            if (variables == null)
            {
                variables = new QueryVariables();
            }
            return Compile(new QueryRequestContext(new QueryRequest { Query = query, Variables = variables }, authService, user));
        }
        public GraphQLDocument Compile(QueryRequest query, IGqlAuthorizationService authService = null, ClaimsPrincipal user = null)
        {
            return Compile(new QueryRequestContext(query, authService, user));
        }
        public GraphQLDocument Compile(QueryRequestContext context)
        {
            var parser = new Utf8GraphQLParser(Encoding.UTF8.GetBytes(context.Query.Query), ParserOptions.Default);
            DocumentNode document = parser.Parse();
            var walker = new EntityGraphQLQueryWalker(schemaProvider, context);
            walker.Visit(document, null);
            return walker.Document;
        }
    }
}