using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Moq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace EchoServer.Tests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        private Mock<TextWriter> _consoleOutputMock;
        private string _testHost;
        private int _testPort;
        private const string Host = "127.0.0.1";
        private const int Port = 9000;

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
            Assert.Multiple(() =>
            {
                Assert.That(GetPrivateField<string>(sender, "_host"), Is.EqualTo(_testHost));
                Assert.That(GetPrivateField<int>(sender, "_port"), Is.EqualTo(_testPort));
                Assert.That(GetPrivateField<UdpClient>(sender, "_udpClient"), Is.Not.Null);
            });
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
            Assert.That(timer, Is.Not.Null);
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
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null! });

            // Assert
            var newCounter = GetPrivateField<ushort>(sender, "_counter");
            Assert.That(newCounter, Is.EqualTo(initialCounter + 1));
            _consoleOutputMock.Verify(x => x.WriteLine($"Message sent to {_testHost}:{_testPort} "), Times.Once());
        }

        [Test]
        public void SendMessageCallback_HandlesExceptions()
        {
            // Arrange
            using var sender = new UdpTimedSender("invalid_host", 99999); 
            var initialCounter = GetPrivateField<ushort>(sender, "_counter");

            // Act
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null! });

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
            InvokePrivateMethod(sender, "SendMessageCallback", new object[] { null! });

            // Assert
            var newCounter = GetPrivateField<ushort>(sender, "_counter");
            Assert.That(newCounter, Is.EqualTo(initialCounter + 1));

            _consoleOutputMock.Verify(x => x.WriteLine($"Message sent to {_testHost}:{_testPort} "), Times.Once());

            Assert.That(GetPrivateField<bool>(udpClient, "_disposed"), Is.False);
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
            Assert.That(timerAfter,Is.Null);
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
            Assert.That(timer, Is.Null);

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
        public async Task IntegrationTest_CompleteWorkflow()
        {
            // Arrange
            using var sender = new UdpTimedSender(_testHost, _testPort);

            // Act & Assert 
            Assert.DoesNotThrow(() => sender.StartSending(100));
            await Task.Delay(150);
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

        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?? throw new ArgumentException($"Field {fieldName} not found");

            return (T)field.GetValue(obj)!;
        }


        private static void InvokePrivateMethod(object obj, string methodName, object[] parameters)
        {
            var method = obj.GetType().GetMethod(methodName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(obj, parameters);
        }
        [Test]
        public void StopSending_WhenTimerExists_DisposesAndSetsNull()
        {
            var sender = new UdpTimedSender(Host, Port);

            var timerField = typeof(UdpTimedSender).GetField("_timer",
                BindingFlags.NonPublic | BindingFlags.Instance);

            timerField?.SetValue(sender, new Timer(_ => { }, null, 100, 100));

            // act
            sender.StopSending();

            // assert
            Assert.That(timerField?.GetValue(sender), Is.Null);
        }

        [Test]
        public void StopSending_WhenTimerIsNull_DoesNotThrow()
        {
            var sender = new UdpTimedSender(Host, Port);

            Assert.DoesNotThrow(() => sender.StopSending());
        }

        [Test]
        public void Dispose_DisposesUdpClientAndTimer_NoExceptions()
        {
            var sender = new UdpTimedSender(Host, Port);

            var timerField = typeof(UdpTimedSender).GetField("_timer",
                BindingFlags.NonPublic | BindingFlags.Instance);

            timerField?.SetValue(sender, new Timer(_ => { }, null, 100, 100));

            // Act
            var udpField = typeof(UdpTimedSender).GetField("_udpClient",
                BindingFlags.NonPublic | BindingFlags.Instance);

            udpField?.SetValue(sender, new UdpClient());

            Assert.DoesNotThrow(() => sender.Dispose());

            // Assert
            Assert.That(timerField?.GetValue(sender), Is.Null);
        }
        [Test]
        public void Constructor_SetsHostField()
        {
            var sender = new UdpTimedSender(Host, Port);

            var hostField = typeof(UdpTimedSender).GetField("_host",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var value = hostField?.GetValue(sender);

            Assert.That(value, Is.EqualTo(Host));
        }

        [Test]
        public void Constructor_SetsPortField()
        {
            var sender = new UdpTimedSender(Host, Port);

            var portField = typeof(UdpTimedSender).GetField("_port",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var value = portField?.GetValue(sender);

            Assert.That(value, Is.EqualTo(Port));
        }

        [Test]
        public void Constructor_CreatesUdpClient()
        {
            var sender = new UdpTimedSender(Host, Port);

            var udpField = typeof(UdpTimedSender).GetField("_udpClient",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var value = udpField?.GetValue(sender);

            Assert.That(value, Is.Not.Null);
            Assert.That(value, Is.InstanceOf<UdpClient>());
        }
        [Test]
        public void StartSending_WhenTimerIsNull_CreatesTimer()
        {
            var sender = new UdpTimedSender(Host, Port);

            sender.StartSending(150);

            var timerField = typeof(UdpTimedSender).GetField("_timer",
                BindingFlags.NonPublic | BindingFlags.Instance);

            var timer = timerField?.GetValue(sender);


            Assert.That(timer, Is.Not.Null);
            Assert.That(timer, Is.InstanceOf<Timer>());
        }

        [Test]
        public void StartSending_WhenAlreadyRunning_ThrowsInvalidOperationException()
        {
            var sender = new UdpTimedSender(Host, Port);

            // Arrange
            var timerField = typeof(UdpTimedSender).GetField("_timer",
                BindingFlags.NonPublic | BindingFlags.Instance);

            timerField?.SetValue(sender, new Timer(_ => { }, null, 100, 100));

            Assert.Throws<InvalidOperationException>(() =>
            {
                sender.StartSending(200);
            });
        }
        [Test]
        public void SendMessageCallback_NormalFlow_IncrementsCounterAndBuildsMsg()
        {
            var sender = new UdpTimedSender(Host, Port);

            var counterField = typeof(UdpTimedSender)
                .GetField("_counter", BindingFlags.NonPublic | BindingFlags.Instance);

            ushort before = (ushort)(counterField?.GetValue(sender) ?? throw new InvalidOperationException("Counter field is null"));

            var method = typeof(UdpTimedSender)
                .GetMethod("SendMessageCallback", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.DoesNotThrow(() => method?.Invoke(sender, new object[] { null! }));

            ushort after = (ushort)(counterField.GetValue(sender) ?? throw new InvalidOperationException("Counter field is null"));
            Assert.That(after, Is.EqualTo(before + 1));
        }

        [Test]
        public void SendMessageCallback_InvalidHost_ExecutesCatchBlock_NoException()
        {
            var sender = new UdpTimedSender("256.0.0.1", Port);

            var method = typeof(UdpTimedSender)
                .GetMethod("SendMessageCallback", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.DoesNotThrow(() => method?.Invoke(sender, new object[] { null! }));
        }

    }
}