using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EchoServer.Tests
{
    public class EchoServerTests
    {
        private EchoServer _server;
        private int _port;

        [SetUp]
        public void Setup()
        {
            // Arrange
            var rand = new Random();
            _port = rand.Next(15000, 25000);

            _server = new EchoServer(_port);

            // Arrange
            _ = Task.Run(async () => await _server.StartAsync());
        }

        [TearDown]
        public void Teardown()
        {
            // Arrange
            _server.Stop();
        }

        [Test]
        public async Task EchoServer_ShouldEchoMessageBack()
        {
            await Task.Delay(200);

            // Arrange
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", _port);

                var stream = client.GetStream();
                string message = "Hello server!";
                byte[] data = Encoding.UTF8.GetBytes(message);

                // Act
                await stream.WriteAsync(new ReadOnlyMemory<byte>(data));

                // Act
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(new Memory<byte>(buffer));

                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Assert
                Assert.That(response, Is.EqualTo(message));
            }
        }

        [Test]
        public async Task EchoServer_ShouldHandleMultipleMessages()
        {
            await Task.Delay(200);

            // Arrange
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync("127.0.0.1", _port);
                var stream = client.GetStream();

                for (int i = 0; i < 3; i++)
                {
                    string msg = $"Message {i}";
                    byte[] send = Encoding.UTF8.GetBytes(msg);

                    // Act
                    await stream.WriteAsync(new ReadOnlyMemory<byte>(send));

                    // Act
                    byte[] buffer = new byte[1024];
                    int read = await stream.ReadAsync(new Memory<byte>(buffer));

                    string response = Encoding.UTF8.GetString(buffer, 0, read);

                    // Assert
                    Assert.That(response, Is.EqualTo(msg));
                }
            }
        }

        [Test]
        public async Task EchoServer_ShouldNotCrash_WhenClientDisconnects()
        {
            await Task.Delay(200);

            // Arrange
            TcpClient client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", _port);

            // Act
            client.Close();

            await Task.Delay(200);

            // Assert
            Assert.Pass();
        }

        [Test]
        public async Task EchoServer_ShouldEchoLargeMessage()
        {
            await Task.Delay(200);

            // Arrange
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", _port);
            var stream = client.GetStream();

            string message = new string('X', 5000);
            byte[] data = Encoding.UTF8.GetBytes(message);

            // Act
            await stream.WriteAsync(new ReadOnlyMemory<byte>(data));

            // Act
            byte[] buffer = new byte[6000];
            int read = await stream.ReadAsync(new Memory<byte>(buffer));

            string response = Encoding.UTF8.GetString(buffer, 0, read);

            // Assert
            Assert.That(response, Is.EqualTo(message));
        }

        [Test]
        public async Task EchoServer_ShouldHandleMultipleClientsInParallel()
        {
            await Task.Delay(200);

            // Arrange
            async Task<string> SendMessageAsync(string text)
            {
                using var client = new TcpClient();
                await client.ConnectAsync("127.0.0.1", _port);
                var stream = client.GetStream();

                byte[] sendData = Encoding.UTF8.GetBytes(text);

                // Act
                await stream.WriteAsync(new ReadOnlyMemory<byte>(sendData));

                // Act
                byte[] buffer = new byte[1024];
                int read = await stream.ReadAsync(new Memory<byte>(buffer));

                return Encoding.UTF8.GetString(buffer, 0, read);
            }

            // Act
            var t1 = SendMessageAsync("client1");
            var t2 = SendMessageAsync("client2");
            var t3 = SendMessageAsync("client3");

            var responses = await Task.WhenAll(t1, t2, t3);

            // Assert
            Assert.That(responses[0], Is.EqualTo("client1"));
            Assert.That(responses[1], Is.EqualTo("client2"));
            Assert.That(responses[2], Is.EqualTo("client3"));
        }

        [Test]
        public async Task EchoServer_ShouldEchoAfterIdlePeriod()
        {
            await Task.Delay(200);

            // Arrange
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", _port);
            var stream = client.GetStream();

            await Task.Delay(300);

            string msg = "Ping";
            byte[] send = Encoding.UTF8.GetBytes(msg);

            // Act
            await stream.WriteAsync(new ReadOnlyMemory<byte>(send));

            // Act
            byte[] buffer = new byte[1024];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, read);

            // Assert
            Assert.That(response, Is.EqualTo(msg));
        }

        [Test]
        public async Task EchoServer_ShouldIgnoreEmptyWrite()
        {
            await Task.Delay(200);

            // Arrange
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", _port);
            var stream = client.GetStream();

            byte[] empty = Array.Empty<byte>();

            // Act
            await stream.WriteAsync(new ReadOnlyMemory<byte>(empty));

            var readTask = stream.ReadAsync(new byte[1024], 0, 1024);
            bool completed = await Task.WhenAny(readTask, Task.Delay(150)) == readTask;

            // Assert
            Assert.That(completed, Is.False);
        }
        private const int TestPort = 12346;

        [Test]
        public void Constructor_CreatesFields()
        {
            var server = new EchoServer(TestPort);

            var portField = typeof(EchoServer).GetField("_port", BindingFlags.NonPublic | BindingFlags.Instance);
            var ctsField = typeof(EchoServer).GetField("_cancellationTokenSource", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(portField?.GetValue(server), Is.EqualTo(TestPort));
            Assert.That(ctsField?.GetValue(server), Is.Not.Null);
        }

    }
}
