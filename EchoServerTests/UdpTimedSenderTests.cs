using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace EchoServer.Tests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private Mock<TextWriter> _consoleOutputMock;
        private string _testHost;
        private int _testPort;

        [SetUp]
        public void SetUp()
        {
            _consoleOutputMock = new Mock<TextWriter>();
            Console.SetOut(_consoleOutputMock.Object);
            _testHost = "127.0.0.1";
            _testPort = 60000;
        }

        [Test]
        public void Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            using var sender = new UdpTimedSender(_testHost, _testPort);

            // Assert
            Assert.AreEqual(_testHost, GetPrivateField<string>(sender, "_host"));
            Assert.AreEqual(_testPort, GetPrivateField<int>(sender, "_port"));
            Assert.IsNotNull(GetPrivateField<UdpClient>(sender, "_udpClient"));
        }

        [Test]
        public void StartSending_InitializesTimer()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);
            var interval = 1000;

            // Act
            sender.StartSending(interval);

            // Assert
            var timer = GetPrivateField<Timer>(sender, "_timer");
            Assert.IsNotNull(timer);
        }

        [Test]
        public void StartSending_ThrowsWhenAlreadyRunning()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);
            var interval = 1000;
            sender.StartSending(interval);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => sender.StartSending(interval));
        }

        [Test]
        public void SendMessageCallback_SendsMessageAndIncrementsCounter()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);
            var initialCounter = GetPrivateField<ushort>(sender, "_counter");

            // Act
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null });

            // Assert
            var newCounter = GetPrivateField<ushort>(sender, "_counter");
            Assert.AreEqual(initialCounter + 1, newCounter);
            _consoleOutputMock.Verify(x => x.WriteLine($"Message sent to {_testHost}:{_testPort} "), Times.Once());
        }

        [Test]
        public void SendMessageCallback_HandlesExceptions()
        {
            // Arrange
            using var sender = new UdpTimedSender("invalid_host", 99999); 
            var initialCounter = GetPrivateField<ushort>(sender, "_counter");

            // Act
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null });

            // Assert
            _consoleOutputMock.Verify(x => x.WriteLine(It.Is<string>(s => s.StartsWith("Error sending message:"))), Times.Once());
        }

        [Test]
        public void SendMessageCallback_CreatesCorrectMessageStructure()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);
            var initialCounter = GetPrivateField<ushort>(sender, "_counter");

            var udpClient = GetPrivateField<UdpClient>(sender, "_udpClient");

            // Act
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null });

            // Assert
            var newCounter = GetPrivateField<ushort>(sender, "_counter");
            Assert.AreEqual(initialCounter + 1, newCounter);

            _consoleOutputMock.Verify(x => x.WriteLine($"Message sent to {_testHost}:{_testPort} "), Times.Once());

            Assert.IsFalse(GetPrivateField<bool>(udpClient, "_disposed"));
        }
        [Test]
        public void StopSending_DisposesTimer()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);
            sender.StartSending(1000);
            var timerBefore = GetPrivateField<Timer>(sender, "_timer");

            // Act
            sender.StopSending();

            // Assert
            var timerAfter = GetPrivateField<Timer>(sender, "_timer");
            Assert.IsNull(timerAfter);
        }

        [Test]
        public void StopSending_DoesNotThrowWhenNotStarted()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);

            // Act & Assert
            Assert.DoesNotThrow(() => sender.StopSending());
        }

        [Test]
        public void Dispose_CallsStopSendingAndDisposesResources()
        {
            // Arrange
            var sender = new UdpTimedSender(_testHost, _testPort);
            sender.StartSending(1000);
            var udpClient = GetPrivateField<UdpClient>(sender, "_udpClient");

            // Act
            sender.Dispose();

            // Assert 
            var timer = GetPrivateField<Timer>(sender, "_timer");
            Assert.IsNull(timer);

            Assert.Throws<ObjectDisposedException>(() =>
            {
                try
                {
                    udpClient.Send(new byte[] { 1 }, 1, new IPEndPoint(IPAddress.Loopback, 1234));
                }
                catch (ObjectDisposedException)
                {
                    throw;
                }
                catch
                {
                }
            });
        }

        [Test]
        public void IntegrationTest_CompleteWorkflow()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);

            // Act & Assert 
            Assert.DoesNotThrow(() => sender.StartSending(100));
            Thread.Sleep(150); 
            Assert.DoesNotThrow(() => sender.StopSending());
            Assert.DoesNotThrow(() => sender.Dispose());
        }

        [Test]
        public void MultipleDispose_Calls_DoNotThrow()
        {
            // Arrange
            var sender = new UdpTimedSender(_testHost, _testPort);

            // Act & Assert
            Assert.DoesNotThrow(() => sender.Dispose());
            Assert.DoesNotThrow(() => sender.Dispose()); 
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }

        private void InvokePrivateMethod(object obj, string methodName, object[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(obj, parameters);
        }
    }
}