---
sidebar_position: 11
---

# Subscriptions

[GraphQL subscriptions](https://spec.graphql.org/October2021/#sec-Subscription) outline an agreed way for services to define events & clients to subscribe to events with the familar GraphQL queries.

:::info

The GraphQL spec does not define how a server should implement this functionality. EntityGraphQL implements the [graphql-ws](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md) protocol. This uses web sockets to deliver messages between the client & server. This is well supported by libraries like [Apollo](https://www.apollographql.com/docs/react/data/subscriptions/).

:::

In EntityGraphQL subscriptions are defined similarly to how mutations are except the return value should be an `IObservable<T>`. When EntityGraphQL receives a subscription operation from a client, it will
- execute the registed subscription method
- subscribe to that result of that method (the `IObservable<T>`)
- on receiving new data from the `IObservable<T>` EntityGraphQL will execute the field selection over the data and publish new data to client

## Chat Example

Let's look at a simple chat event. You can see the full code in the [subscription example](https://github.com/EntityGraphQL/EntityGraphQL/tree/master/src/examples/subscriptions) on GitHub.

```cs
public class ChatSubscriptions
{
    [GraphQLSubscription("Subscription to new messages")]
    public IObservable<Message> OnMessage(ChatService chat)
    {
        return chat.Subscribe();
    }
}
```

Here is a simple `ChatService` that uses that the EntityGraphQL supplied `Broadcaster<T>` class to broadcast new events to any subscribers. The `Broadcast<T>.Subscribe()` returns an object that implements `IObservable<T>` which is what the subscription registration method above returns. You could also implement your own `IObservable<T>`, internally EntityGraphQL calls `Subscribe()` on the result.

Here is our simple chat service that handles receiving new messages and broadcasting them.

```cs
public class ChatService
{
    // highlight-next-line
    private readonly Broadcaster<Message> broadcaster = new();

    public Message PostMessage(ChatContext db, string message, string user)
    {
        var msg = new Message
        {
            Id = Guid.NewGuid(),
            Text = message,
            Timestamp = DateTime.Now,
            UserId = user,
        };

        db.Messages.Add(msg);
        db.SaveChanges();

        broadcaster.OnNext(msg);

        return msg;
    }
    // highlight-start
    public IObservable<Message> Subscribe()
    {
        return broadcaster;
    }
    // highlight-end
}
```

Setting up our application - register our schema, enable web sockets and GraphQLWebSockets

```cs
// ...
builder.Services.AddSingleton<ChatService>();
builder.Services.AddGraphQLSchema<ChatContext>(options =>
{
    options.ConfigureSchema = (schema) => {
        schema.Mutation().AddFrom<ChatMutations>();
        // highlight-next-line
        schema.Subscription().AddFrom<ChatSubscriptions>();
    }
});

var app = builder.Build();
// ...

// highlight-next-line
app.UseWebSockets();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapGraphQL<ChatContext>();
});
// highlight-next-line
app.UseGraphQLWebSockets<ChatContext>();

app.Run();

```

We will use a mutation to allow clients to post messages as well, it uses the `ChatService` to post new messages, which as you see above broadcasts new messages to subscribers.

```cs
public class ChatMutations
{
    [GraphQLMutation]
    public static Expression<Func<ChatContext, Message>> PostMessage(ChatContext db, string message, string user, ChatService chat)
    {
        var postedMessage = chat.PostMessage(db, message, user);
        return ctx => ctx.Messages.First(message => message.Id == postedMessage.Id);
    }
}
```

Now when a client uses the `postMessage` mutation (which triggers `ChatService.PostMessage()`), or anything else in the server that triggers `ChatService.PostMessage()`, subscribers will receive the new data (only the fields they requested).

## What is Happening

Following the [`graphql-ws` protocol](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md), once a client has sent the `connection_init` message and recieved the `connection_ack` message back it can send a `subscribe` message with a unique ID and the payload is the GraphQL subscription operation:

```graphql
subscription ChatRoom {
  onMessage {
    id
    user
    text
    timestamp
  }
}
```

:::info
The `graphql-ws` protocol uses messages to communicate over the web socket connection it has with the server. Multiple subscriptions may be maintained over that connection this way using a unique ID for each subscription.
:::

When EntityGraphQL executes the above graphql it receives the `IObservable<T>` as the result. No data is sent to the client at this stage. The `IObservable<T>` result is held with other metadata (like the schema, connection) in a `WebSocketSubscription<TQueryContext>` object that represents a single subscription on a web socket connection. That connection may have multiple subscriptions.

`WebSocketSubscription<TQueryContext>` subscribes to the `IObservable<T>` and on new data it executes the GraphQL field selection over that data and sends the result to the cient over the web socket connection as the payload of a `next` message containing the ID for the subscription.

## Load Balances and Lambdas/Functions

In the above chat example we have a way to post new messages and have those new messages published to all clients subscribed. You may be wondering about events triggered on different servers or from other services. For example if you use load balancers to scale, a `postMessage` may be delivered to server `A` while some clients have a web socket connection to server `B`. You will need a way to have server `A` tell server `B` that there is new data so it can notify the subscribers.

This is why the GraphQL specificaiton does not dictate implemenation details!

A typical implementation of this pattern is using a stream or queue service where your servers or services post events to that and listen for events too. EntityGraphQL does not aim to implement a fully distributed queue system (there ae many great choices). It aims to provide the glue to support the GraphQL subscription speficiation.

If you are hosting a ASP.NET application behind a load balancer you could have `ChatService` post message events to a stream/queue, have your application (on each server it is running) subscribe to events from the stream/queue which can then broadcast that event to any subscribers connected.

If you are using lambdas/functions you will have to do a similar thing with a stream or queue with the added complexity of handling web sockets through an API Gateway or similar.

:::tip
Got ideas that would help users implement these patterns? Please reach out or open a PR with examples!
:::

## Query or Mutation over Web Sockets

The [`graphql-ws`](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md#single-result-operation) protocol also supports executing GraphQL `query` and `mutation` operations over the connection. This is supported by EntityGraphQL and follows the speficiation. However typically clients will use HTTPS for `query` and `mutation` operations and web sockets for `subscription` operations to aid with scale.

## Other Implementations

The above GraphQL subscription implemenation is based on [`graphql-ws`](https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md) protocol which uses web sockets. It is provided by the [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) package. The base [EntityGraphQL](https://www.nuget.org/packages/EntityGraphQL) package does not know about web sockets, there is potential to implement other transpost layers or protocols on top of that.

The result you will get from executing a subscription statement as above is the `GraphQLSubscribeResult` object that looks like this:

```cs
public class GraphQLSubscribeResult
{
    // The T in IObservable<T> - the data we are returning
    Type EventType { get; }
    // Used to call .ExecuteSubscriptionEvent() when you have new data
    GraphQLSubscriptionStatement SubscriptionStatement { get; }
    // Passed to the ExecuteSubscriptionEvent() call
    GraphQLSubscriptionField Field { get; }
    // Returns the IObservable<T> object. This is an object because at compile time we can't just cast the IObservable<T> as T is not known to us. See GraphQLSubscriptionStatement.ExecuteAsync() to see where thisi s created
    object GetObservable();
}
```

You will be able to subscribe to the `IObservable<T>` and execute the selection when you have new data available with `SubscriptionStatement` and `Field`.
