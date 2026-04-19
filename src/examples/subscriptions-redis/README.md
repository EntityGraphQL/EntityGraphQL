# GraphQL Subscriptions with Redis Pub/Sub

Demonstrates how to fan out GraphQL subscription events across multiple server instances using Redis Pub/Sub. When a message is posted to **any** instance, every connected WebSocket client on **every** instance receives the event — the foundation for horizontally-scaled real-time APIs.

## Why Redis?

The built-in `Broadcaster<T>` is in-process only. In a load-balanced deployment, a mutation hitting Instance A would only notify clients connected to Instance A. By publishing to a Redis channel and having every instance subscribe to the same channel, the fan-out becomes cross-process.

```
Client A ──ws──► Instance 1 ◄── Redis channel ──► Instance 2 ──ws──► Client B
                       │                               │
                  mutation POST ──────────────────────►│ (redis publish)
```

## Async subscription setup

`ChatSubscriptions.OnMessage` returns `Task<IObservable<Message>>` rather than `IObservable<Message>` directly. EntityGraphQL supports this pattern so that subscription setup can be asynchronous — useful when connecting to a broker requires an awaitable operation (here: a Redis PING to verify connectivity before accepting the subscription).

## Prerequisites

- .NET 8 SDK
- Redis (local or Docker)

```bash
# Docker — start a local Redis on the default port
docker run -d -p 6379:6379 redis:latest
```

## Run two instances

Open two terminals in this directory:

```bash
# Terminal 1 — Instance 1
dotnet run --launch-profile Instance1

# Terminal 2 — Instance 2
dotnet run --launch-profile Instance2
```

## Test cross-instance fan-out

Open the GraphQL playground on each instance (default at `/graphql`).

**Instance 1 (port 5300) — subscribe:**
```graphql
subscription {
  onMessage {
    id
    text
    timestamp
    userId
  }
}
```

**Instance 2 (port 5301) — post a message:**
```graphql
mutation {
  postMessage(message: "Hello from Instance 2!", user: "alice") {
    id
    text
  }
}
```

The subscription on Instance 1 will receive the event even though the mutation
was sent to Instance 2.

## Key files

| File | Purpose |
|---|---|
| `Redis/RedisObservable.cs` | `IObservable<T>` backed by a Redis Pub/Sub channel |
| `Services/ChatService.cs` | Publishes to Redis; returns `Task<IObservable<Message>>` |
| `Subscriptions/ChatSubscriptions.cs` | GraphQL subscription using async return type |
| `Mutations/ChatMutations.cs` | Async mutation that publishes via Redis |
| `appsettings.json` | Redis connection string (`ConnectionStrings:Redis`) |
