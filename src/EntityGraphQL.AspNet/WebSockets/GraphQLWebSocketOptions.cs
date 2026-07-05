using System;

namespace EntityGraphQL.AspNet.WebSockets;

/// <summary>
/// Options controlling protocol-level behaviour of the GraphQL WebSocket server.
/// </summary>
public class GraphQLWebSocketOptions
{
    /// <summary>
    /// Maximum size in bytes of a single incoming WebSocket message. A message exceeding this closes the
    /// connection with close code 1009 (MessageTooBig). Protects against a client streaming an unbounded
    /// message into server memory. Default 1 MB. Set to null for no limit.
    /// </summary>
    public long? MaxMessageSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// How long the client has after connecting to send its connection_init message. If it does not, the
    /// connection is closed with close code 4408 (Connection initialisation timeout) per the graphql-ws
    /// protocol, releasing sockets held open by idle or misbehaving clients. Default 10 seconds.
    /// Set to null for no timeout.
    /// </summary>
    public TimeSpan? ConnectionInitTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
