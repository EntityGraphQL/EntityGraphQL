namespace EntityGraphQL.AspNet.WebSockets
{
    /// <summary>
    /// Protocol message type.
    /// </summary>
    public static class GraphQLWSMessageType
    {
        public const string ConnectionInit = "connection_init";
        public const string ConnectionAck = "connection_ack";
        public const string Ping = "ping";
        public const string Pong = "pong";
        public const string Error = "error";
        public const string Complete = "complete";
        public const string Subscribe = "subscribe";
        public const string Next = "next";
    }
}