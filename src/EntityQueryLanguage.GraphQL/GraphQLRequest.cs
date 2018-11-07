namespace EntityQueryLanguage.GraphQL
{
    public class GraphQLRequest
    {
        public string OperationName { get; set; }
        public string Query { get; set; }
        public QueryVariables Variables { get; set; }
    }

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