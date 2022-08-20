using System.Net.WebSockets;
using EntityGraphQL.AspNet.WebSockets;
using EntityGraphQL.Schema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace EntityGraphQL.AspNet.Tests
{
    /// <summary>
    /// Main focus is testing the websocket server responds to the client events correctly, does the correct errors etc
    /// 
    /// Remember:
    ///     ReceiveAsync is data the GraphQLWebSocketServer is receiving from the client.
    ///     SendAsync is data the GraphQLWebSocketServer is sending to the client.
    /// </summary>
    public class GraphQLWebSocketServerTests
    {
        private static (GraphQLWebSocketServer<TestQueryContext>, MockSocket, Mock<HttpContext>) Setup()
        {
            var httpContext = SetupMockHttpContext();
            var socket = new MockSocket();
            var server = new GraphQLWebSocketServer<TestQueryContext>(socket.Object, httpContext.Object);
            return (server, socket, httpContext);
        }

        [Fact]
        public async void TestConnectionInitGetAcknowledgement()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            // next read close the socked to end things
            socket.InSequence(sequence).SetupReceiveCloseAsync();

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync((int)WebSocketCloseStatus.NormalClosure, "Test over");

            await server.HandleAsync();
        }

        [Fact]
        public async void TestTooManyConnectionInitError()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            // client sends again 
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync(4429, "Too many initialisation requests");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestPingSendsPong()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.PING}\"}}");
            // next read close the socked to end things
            socket.InSequence(sequence).SetupReceiveCloseAsync();

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.PONG}\"}}");

            socket.SetupAndAssertCloseAsync((int)WebSocketCloseStatus.NormalClosure, "Test over");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestSubscribeNoAck()
        {
            var (server, socket, _) = Setup();

            socket.SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.SUBSCRIBE}\"}}");

            socket.SetupAndAssertCloseAsync(4401, "Unauthorized");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestSubscribeInvalidNoId()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.SUBSCRIBE}\"}}");

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync(4400, "Invalid subscribe message, missing id field.");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestSubscribeInvalidNoPayload()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"id\":\"{Guid.NewGuid()}\",\"type\":\"{GraphQLWSMessageType.SUBSCRIBE}\"}}");

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync(4400, "Invalid subscribe message, missing payload field.");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestSubscribeSuccess()
        {
            var (server, socket, _) = Setup();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            socket.InSequence(sequence).SetupReceiveAsync(new GraphQLWSRequest
            {
                Id = Guid.NewGuid(),
                Type = GraphQLWSMessageType.SUBSCRIBE,
                Payload = new QueryRequest
                {
                    Query = "subscription DoIt { onMessage { text } }"
                }
            });
            socket.InSequence(sequence).SetupReceiveCloseAsync();

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync((int)WebSocketCloseStatus.NormalClosure, "Test over");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestSubscribeSameId()
        {
            var (server, socket, _) = Setup();
            var id = Guid.NewGuid();

            var sequence = new MockSequence();
            socket.InSequence(sequence).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            socket.InSequence(sequence).SetupReceiveAsync(new GraphQLWSRequest
            {
                Id = id,
                Type = GraphQLWSMessageType.SUBSCRIBE,
                Payload = new QueryRequest
                {
                    Query = "subscription DoIt { onMessage { text } }"
                }
            });
            socket.InSequence(sequence).SetupReceiveAsync(new GraphQLWSRequest
            {
                Id = id,
                Type = GraphQLWSMessageType.SUBSCRIBE,
                Payload = new QueryRequest
                {
                    Query = "subscription DoIt { onMessage { text } }"
                }
            });

            socket.SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");

            socket.SetupAndAssertCloseAsync(4409, $"Subscriber for {id} already exists");

            await server.HandleAsync();
        }
        [Fact]
        public async void TestNextIsSent()
        {
            var (server, socket, httpContext) = Setup();
            var id = Guid.NewGuid();
            var chatService = httpContext.Object.RequestServices.GetService<TestChatService>()!;

            var recvSeq = new MockSequence();
            socket.InSequence(recvSeq).SetupReceiveAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_INIT}\"}}");
            socket.InSequence(recvSeq).SetupReceiveAsync(new GraphQLWSRequest
            {
                Id = id,
                Type = GraphQLWSMessageType.SUBSCRIBE,
                Payload = new QueryRequest
                {
                    Query = "subscription DoIt { onMessage { text } }"
                }
            }, () =>
            {
                // this runs after server has received the message
                // We don't know whenthe execution of the subscribe finishes though so we wait a bit :(
                Task.Delay(100).Wait();
                chatService.PostMessage("Hello");
            });
            socket.InSequence(recvSeq).SetupReceiveCloseAsync();

            var sendSeq = new MockSequence();
            socket.InSequence(sendSeq).SetupAndAssertSendAsync($"{{\"type\":\"{GraphQLWSMessageType.CONNECTION_ACK}\"}}");
            socket.InSequence(sendSeq).SetupAndAssertSendAsync($"{{\"payload\":{{\"data\":{{\"onMessage\":{{\"text\":\"Hello\"}}}}}},\"id\":\"{id}\",\"type\":\"{GraphQLWSMessageType.NEXT}\"}}");

            socket.SetupAndAssertCloseAsync((int)WebSocketCloseStatus.NormalClosure, "Test over");

            await server.HandleAsync();
        }

        private static Mock<HttpContext> SetupMockHttpContext()
        {
            var chatService = new TestChatService();
            var schemaContext = new TestQueryContext();
            var schema = SchemaBuilder.FromObject<TestQueryContext>();
            schema.AddType<Message>("Message data").AddAllFields();
            schema.Subscription().AddFrom<TestSubscription>();
            var httpContext = new Mock<HttpContext>();
            var servicesMock = new Mock<IServiceProvider>();
            servicesMock.Setup(sp => sp.GetService(typeof(SchemaProvider<TestQueryContext>))).Returns(schema);
            servicesMock.Setup(sp => sp.GetService(typeof(TestQueryContext))).Returns(schemaContext);
            servicesMock.Setup(sp => sp.GetService(typeof(TestChatService))).Returns(chatService);
            httpContext.Setup(c => c.RequestServices).Returns(servicesMock.Object);
            return httpContext;
        }
    }
}