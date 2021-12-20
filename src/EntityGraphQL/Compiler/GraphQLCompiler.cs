using EntityGraphQL.Compiler.EntityQuery;
using EntityGraphQL.Schema;
using HotChocolate.Language;
using System.Text;

namespace EntityGraphQL.Compiler
{
    public class GraphQLCompiler
    {
        private readonly ISchemaProvider schemaProvider;
        private readonly IMethodProvider methodProvider;
        public GraphQLCompiler(ISchemaProvider schemaProvider, IMethodProvider methodProvider)
        {
            this.schemaProvider = schemaProvider;
            this.methodProvider = methodProvider;
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
        public GraphQLDocument Compile(string query, QueryVariables variables = null, UserAuthInfo authInfo = null)
        {
            if (variables == null)
            {
                variables = new QueryVariables();
            }
            return Compile(new QueryRequest { Query = query, Variables = variables }, authInfo);
        }
        public GraphQLDocument Compile(QueryRequest request, UserAuthInfo authInfo = null)
        {
            var parser = new Utf8GraphQLParser(Encoding.UTF8.GetBytes(request.Query), ParserOptions.Default);
            DocumentNode document = parser.Parse();
            var walker = new EntityGraphQLQueryWalker(schemaProvider, request.Variables, authInfo);
            walker.Visit(document, null);
            return walker.Document;
        }
    }
}