using NUnit.Framework;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

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
                await stream.WriteAsync(data, 0, data.Length);

                // Act
                byte[] buffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                // Assert
                Assert.AreEqual(message, response);
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
                    await stream.WriteAsync(send, 0, send.Length);

                    // Act
                    byte[] buffer = new byte[1024];
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length);

                    string response = Encoding.UTF8.GetString(buffer, 0, read);

                    // Assert
                    Assert.AreEqual(msg, response);
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
            await stream.WriteAsync(data, 0, data.Length);

            // Act
            byte[] buffer = new byte[6000];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);

            string response = Encoding.UTF8.GetString(buffer, 0, read);

            // Assert
            Assert.AreEqual(message, response);
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
                await stream.WriteAsync(sendData, 0, sendData.Length);

                // Act
                byte[] buffer = new byte[1024];
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);

                return Encoding.UTF8.GetString(buffer, 0, read);
            }

            // Act
            var t1 = SendMessageAsync("client1");
            var t2 = SendMessageAsync("client2");
            var t3 = SendMessageAsync("client3");

            var responses = await Task.WhenAll(t1, t2, t3);

            // Assert
            Assert.AreEqual("client1", responses[0]);
            Assert.AreEqual("client2", responses[1]);
            Assert.AreEqual("client3", responses[2]);
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
            await stream.WriteAsync(send, 0, send.Length);

            // Act
            byte[] buffer = new byte[1024];
            int read = await stream.ReadAsync(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, read);

            // Assert
            Assert.AreEqual(msg, response);
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
            await stream.WriteAsync(empty, 0, empty.Length);

            var readTask = stream.ReadAsync(new byte[1024], 0, 1024);
            bool completed = await Task.WhenAny(readTask, Task.Delay(150)) == readTask;

            // Assert
            Assert.IsFalse(completed);
        }
    }
}
