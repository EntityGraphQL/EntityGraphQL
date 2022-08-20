using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EntityGraphQL.AspNet.WebSockets;
using Moq;
using Moq.Language;
using Xunit;

namespace EntityGraphQL.AspNet.Tests
{
    internal static class MockSocketHelper
    {
        public static void SetupReceiveAsync(this ISetupConditionResult<WebSocket> mock, GraphQLWSRequest request, Action? runAfter = null)
        {
            SetupReceiveAsync(mock, JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }), runAfter);
        }
        public static void SetupReceiveAsync(this ISetupConditionResult<WebSocket> mock, string message, Action? runAfter = null)
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            mock.Setup(s => s.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(buffer.Count, WebSocketMessageType.Text, true, null, null))
                .Callback((ArraySegment<byte> segment, CancellationToken token) =>
                {
                    buffer.CopyTo(segment);
                    if (runAfter != null)
                        Task.Run(runAfter, token);
                });
        }
        public static void SetupReceiveCloseAsync(this ISetupConditionResult<WebSocket> mock)
        {
            mock.Setup(s => s.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, WebSocketCloseStatus.NormalClosure, "Test over"));
        }
        public static void SetupAndAssertSendAsync(this ISetupConditionResult<WebSocket> mock, string expectedMessage)
        {
            var expectedResponse = new ArraySegment<byte>(Encoding.UTF8.GetBytes(expectedMessage));
            mock.Setup(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback((ArraySegment<byte> segment, WebSocketMessageType messageType, bool endOfMessage, CancellationToken token) =>
                {
                    Assert.Equal(expectedResponse, segment);
                });
        }
    }
    internal class MockSocket : Mock<WebSocket>
    {
        private WebSocketCloseStatus? closed = null;

        public MockSocket()
        {
            Setup(s => s.CloseStatus).Returns(() => closed).Verifiable();
            Setup(s => s.State).Returns(() => closed.HasValue ? WebSocketState.Closed : WebSocketState.Open);
        }

        public void SetupReceiveAsync(string message)
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            Setup(s => s.ReceiveAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new WebSocketReceiveResult(buffer.Count, WebSocketMessageType.Text, true, null, null))
                .Callback((ArraySegment<byte> segment, CancellationToken token) =>
                {
                    buffer.CopyTo(segment);
                });
        }

        internal void SetupAndAssertCloseAsync(int closeCode, string closeMessage)
        {
            Setup(s => s.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), CancellationToken.None))
                .Returns(Task.CompletedTask)
                .Callback((WebSocketCloseStatus status, string description, CancellationToken token) =>
                {
                    closed = status;
                    Assert.Equal(closeMessage, description);
                    Assert.Equal(closeCode, (int)status);
                });
        }

        internal void SetupAndAssertSendAsync(string expectedMessage)
        {
            var expectedResponse = new ArraySegment<byte>(Encoding.UTF8.GetBytes(expectedMessage));
            Setup(s => s.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Callback((ArraySegment<byte> segment, WebSocketMessageType messageType, bool endOfMessage, CancellationToken token) =>
                {
                    Assert.Equal(expectedResponse, segment);
                });
        }
    }
}