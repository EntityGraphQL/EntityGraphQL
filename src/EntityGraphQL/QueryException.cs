namespace EntityGraphQL
{
    [System.Serializable]
    public class QueryException : System.Exception
    {
        public QueryException() { }
        public QueryException(string message) : base(message) { }
        public QueryException(string message, System.Exception inner) : base(message, inner) { }
    }
}