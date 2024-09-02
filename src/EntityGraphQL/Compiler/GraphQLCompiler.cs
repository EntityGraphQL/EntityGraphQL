using EntityGraphQL.Schema;
using HotChocolate.Language;

namespace EntityGraphQL.Compiler;

/// <summary>
/// Compiles a Graph QL query document string into an AST for processing.
/// </summary>
public class GraphQLCompiler
{
    private readonly ISchemaProvider schemaProvider;

    public GraphQLCompiler(ISchemaProvider schemaProvider)
    {
        this.schemaProvider = schemaProvider;
    }

    /// Parses a GraphQL-like query syntax into a tree representing the requested object graph. E.g.
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
    public GraphQLDocument Compile(string query, QueryVariables? variables = null)
    {
        if (variables == null)
        {
            variables = new QueryVariables();
        }
        return Compile(new QueryRequest { Query = query, Variables = variables });
    }

    public GraphQLDocument Compile(string query)
    {
        return Compile(new QueryRequest { Query = query });
    }

    public GraphQLDocument Compile(QueryRequest query)
    {
        if (query.Query == null)
            throw new EntityGraphQLCompilerException($"GraphQL Query can not be null");

        DocumentNode document = Utf8GraphQLParser.Parse(query.Query, ParserOptions.Default);
        var walker = new EntityGraphQLQueryWalker(schemaProvider, query.Variables);
        walker.Visit(document, null);
        if (walker.Document == null)
            throw new EntityGraphQLCompilerException($"Error compiling query: {query.Query}");
        return walker.Document;
    }
}
