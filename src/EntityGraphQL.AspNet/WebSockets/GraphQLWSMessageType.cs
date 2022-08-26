namespace EntityGraphQL.AspNet.WebSockets
{
    /// <summary>
    /// Protocol message type.
    /// </summary>
    public static class GraphQLWSMessageType
    {
        public const string CONNECTION_INIT = "connection_init";
        public const string CONNECTION_ACK = "connection_ack";
        public const string PING = "ping";
        public const string PONG = "pong";
        public const string ERROR = "error";
        public const string COMPLETE = "complete";
        public const string SUBSCRIBE = "subscribe";
        public const string NEXT = "next";
    }
}