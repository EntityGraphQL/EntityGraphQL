using System.Collections.Generic;

namespace EntityGraphQL
{
    /// <summary>
    /// A GraphQL request. The query, and any variables
    /// </summary>
    public class QueryRequest
    {
        public string Query { get; set; }
        public QueryVariables Variables { get; set; }
    }

    /// <summary>
    /// Holds the variables passed along with a GraphQL query
    /// </summary>
    /// <typeparam name="string"></typeparam>
    /// <typeparam name="object"></typeparam>
    public class QueryVariables : Dictionary<string, object>
    {
        public object GetValueFor(string varKey)
        {
            return ContainsKey(varKey) ? this[varKey] : null;
        }
    }

    /// <summary>
    /// Describes any errors that might happen while resolving the query request
    /// </summary>
    public class GraphQLError
    {
        private string message;

        public GraphQLError(string message)
        {
            this.Message = message;
        }

        public string Message { get => message; set => message = value; }
    }
}