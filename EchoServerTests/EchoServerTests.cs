using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Moq;
using NUnit.Framework;

namespace EchoServer.Tests
{
    [TestFixture]
    public class EchoServerTests
    {
        [Test]
        public void Constructor_WithPort_ShouldInitialize()
        {
            // Arrange & Act
            var server = new EchoServer(5000);

            // Assert
            Assert.That(server, Is.Not.Null);
        }

        [Test]
        public async Task NetworkHandler_HandleClientAsync_ShouldEchoData()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockTcpClient = new Mock<TcpClient>();
            var mockNetworkStream = new Mock<NetworkStream>(mockTcpClient.Object);

            var networkHandler = new NetworkHandler(mockLogger.Object);

            byte[] testData = new byte[] { 1, 2, 3, 4, 5 };
            var callCount = 0;

            mockTcpClient.Setup(c => c.GetStream()).Returns(mockNetworkStream.Object);
            mockTcpClient.Setup(c => c.Close());

            mockNetworkStream
                .Setup(ns => ns.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[] buffer, int offset, int count, CancellationToken token) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        Array.Copy(testData, 0, buffer, 0, testData.Length);
                        return testData.Length;
                    }
                    return 0; // End of stream
                });

            mockNetworkStream
                .Setup(ns => ns.WriteAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await networkHandler.HandleClientAsync(mockTcpClient.Object, CancellationToken.None);

            // Assert
            mockNetworkStream.Verify(
                ns => ns.WriteAsync(testData, 0, testData.Length, It.IsAny<CancellationToken>()),
            Times.Once);

            mockLogger.Verify(l => l.LogInfo($"Echoed {testData.Length} bytes to the client."), Times.Once);
        }

        [Test]
        public async Task NetworkHandler_WhenClientDisconnects_ShouldCloseClient()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockTcpClient = new Mock<TcpClient>();
            var mockNetworkStream = new Mock<NetworkStream>(mockTcpClient.Object);

            var networkHandler = new NetworkHandler(mockLogger.Object);

            mockTcpClient.Setup(c => c.GetStream()).Returns(mockNetworkStream.Object);
            mockTcpClient.Setup(c => c.Close());

            mockNetworkStream
                .Setup(ns => ns.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0); // Immediate disconnection

            // Act
            await networkHandler.HandleClientAsync(mockTcpClient.Object, CancellationToken.None);

            // Assert
            mockTcpClient.Verify(c => c.Close(), Times.Once);
            mockLogger.Verify(l => l.LogInfo("Client disconnected."), Times.Once);
        }

        [Test]
        public async Task NetworkHandler_WhenReadThrowsException_ShouldLogError()
        {
            // Arrange
            var mockLogger = new Mock<ILogger>();
            var mockTcpClient = new Mock<TcpClient>();
            var mockNetworkStream = new Mock<NetworkStream>(mockTcpClient.Object);

            var networkHandler = new NetworkHandler(mockLogger.Object);

            mockTcpClient.Setup(c => c.GetStream()).Returns(mockNetworkStream.Object);
            mockTcpClient.Setup(c => c.Close());

            mockNetworkStream
                .Setup(ns => ns.ReadAsync(It.IsAny<byte[]>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("Network error"));

            // Act
            await networkHandler.HandleClientAsync(mockTcpClient.Object, CancellationToken.None);

            // Assert
            mockLogger.Verify(l => l.LogError("Error: Network error"), Times.Once);
            mockTcpClient.Verify(c => c.Close(), Times.Once);
        }
    }

    [TestFixture]
    public class UdpTimedSenderTests
    {
        [Test]
        public void StartSending_WhenAlreadyRunning_ShouldThrowException()
        {
            // Arrange
            var sender = new UdpTimedSender("127.0.0.1", 60000);

            // First call should work
            sender.StartSending(1000);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(1000));

            // Cleanup
            sender.StopSending();
            sender.Dispose();
        }
    }
}