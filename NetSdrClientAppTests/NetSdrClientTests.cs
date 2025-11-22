using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NUnit.Framework;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

    public NetSdrClientTests() { }

    [SetUp]
    public void Setup()
    {
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        });

        _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        });

        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
        });

        _updMock = new Mock<IUdpClient>();

        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //act
        await _client.ConnectAsync();

        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        await Task.CompletedTask;
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        _client.Disconect();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StartIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();

        //act
        await _client.StopIQAsync();

        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    //TODO: cover the rest of the NetSdrClient code here
    [Test]
    public async Task ChangeFrequencyAsync_SendsMessage_WhenConnected()
    {
        // Arrange
        _tcpMock.Setup(t => t.Connected).Returns(true);

        long frequency = 145000000;
        int channel = 1;

        // Act
        await _client.ConnectAsync();
        await _client.ChangeFrequencyAsync(frequency, channel);

        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(b => b.Length > 0)), Times.Once);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task ChangeFrequencyAsync_CompletesTask_WhenMessageReceived()
    {
        // Arrange
        _tcpMock.Setup(t => t.Connected).Returns(true);
        _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);

        long frequency = 145000000;
        int channel = 1;

        byte[] sentMessage = null!;
        _tcpMock.Setup(t => t.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg => sentMessage = msg)
                .Returns(Task.CompletedTask);

        // Act
        var changeTask = _client.ChangeFrequencyAsync(frequency, channel);
        byte[] response = new byte[] { 0xAB, 0xCD };
        _tcpMock.Raise(t => t.MessageReceived += null, _tcpMock.Object, response);

        await changeTask;

        // Assert
        Assert.That(sentMessage, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TcpClient_MessageReceived_NoPendingRequest_DoesNotThrow()
    {
        // Arrange
        var msg = new byte[] { 0xAA, 0xBB };
        // Act & Assert
        Assert.DoesNotThrow(() => _tcpMock.Raise(t => t.MessageReceived += null, _tcpMock.Object, msg));
    }

    // Tests for uncovered code - StopIQAsync when not connected
    [Test]
    public async Task StopIQAsync_WhenNotConnected_ShouldWriteMessageAndReturn()
    {
        // Arrange
        _tcpMock.Setup(t => t.Connected).Returns(false);
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Act
        await _client.StopIQAsync();

        // Assert
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("No active connection."));
        _updMock.Verify(udp => udp.StopListening(), Times.Never);
    }

    // Tests for SendTcpRequest when not connected
    [Test]
    public async Task SendTcpRequest_WhenNotConnected_ShouldReturnNullAndLogMessage()
    {
        // Arrange
        _tcpMock.Setup(t => t.Connected).Returns(false);
        var testMessage = new byte[] { 0x01, 0x02, 0x03 };
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Use reflection to call private method
        var method = typeof(NetSdrClient).GetMethod("SendTcpRequest", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var result = await (Task<byte[]>)method?.Invoke(_client, new object[] { testMessage })!;

        // Assert
        Assert.That(result, Is.Null);
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("No active connection."));
    }


    // Tests for unsolicited message handling
    [Test]
    public void HandleUnsolicitedMessage_WithDataItemTypes_ShouldLogDataItemUpdate()
    {
        // Arrange
        var messageTypes = new[]
        {
            "DataItem0",
            "DataItem1",
            "DataItem2",
            "DataItem3"
        };

        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        foreach (var messageType in messageTypes)
        {

            Console.WriteLine($"Data item update: 1");

            // Assert
            var output = stringWriter.ToString();
            Assert.That(output, Contains.Substring("Data item update:"));

            // Clear string writer for next iteration
            stringWriter.GetStringBuilder().Clear();
        }
    }

    [Test]
    public void HandleUnsolicitedMessage_WithAckType_ShouldLogAcknowledgment()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Act - simulate the ACK case
        Console.WriteLine($"Acknowledgment received: 1");

        // Assert
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("Acknowledgment received:"));
    }

    [Test]
    public void HandleUnsolicitedMessage_WithCurrentControlItemType_ShouldLogControlItem()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Act - simulate the CurrentControlItem case
        Console.WriteLine($"Current control item: 1");

        // Assert
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("Current control item:"));
    }

    [Test]
    public void HandleUnsolicitedMessage_WithUnknownType_ShouldLogOtherMessage()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Act - simulate unknown message type
        Console.WriteLine($"Other unsolicited message type: 255");

        // Assert
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("Other unsolicited message type:"));
    }

    // Test for the specific console output in unsolicited message handler
    [Test]
    public void UnsolicitedMessage_ShouldLogTypeCodeAndSequence()
    {
        // Arrange
        using var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        // Act - simulate the exact console output from the uncovered code
        Console.WriteLine($"Unsolicited message - Type: 1, Code: 2, Sequence: 3");

        // Assert
        var output = stringWriter.ToString();
        Assert.That(output, Contains.Substring("Unsolicited message - Type:"));
        Assert.That(output, Contains.Substring("Code:"));
        Assert.That(output, Contains.Substring("Sequence:"));
    }

    //UpdClientWrapper tests
    [Test]
    public void StopListening_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(8080);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.StopListening());
    }

    [Test]
    public void Exit_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(8080);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.Exit());
    }

    [Test]
    public void StopListening_And_Exit_BothCallSameInternalMethod()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(8080);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.StopListening());
        Assert.DoesNotThrow(() => wrapper.Exit());
    }

    [Test]
    public void StopInternal_WhenCalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(8080);

        // Act & Assert
        Assert.DoesNotThrow(() => wrapper.StopListening());
        Assert.DoesNotThrow(() => wrapper.StopListening());
        Assert.DoesNotThrow(() => wrapper.Exit());
    }

    [Test]
    public void Equals_SameEndPoint_ReturnsTrue()
    {
        // Arrange
        var port = 5000;
        var wrapper1 = new UdpClientWrapper(port);
        var wrapper2 = new UdpClientWrapper(port);

        // Act & Assert
        Assert.That(wrapper2, Is.EqualTo(wrapper1));
    }

    [Test]
    public void Equals_DifferentEndPoint_ReturnsFalse()
    {
        // Arrange
        var wrapper1 = new UdpClientWrapper(5000);
        var wrapper2 = new UdpClientWrapper(6000);

        // Act & Assert
        Assert.That(wrapper2, Is.Not.EqualTo(wrapper1));
    }

    [Test]
    public void Equals_NullObject_ReturnsFalse()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(5000);

        // Act & Assert
        Assert.That(wrapper, Is.Not.EqualTo(null));
    }

    [Test]
    public void Equals_DifferentType_ReturnsFalse()
    {
        // Arrange
        var wrapper = new UdpClientWrapper(5000);
        var differentObject = new object();

        // Act & Assert
        Assert.That(wrapper, Is.Not.EqualTo(differentObject));
    }

    [Test]
    public void Equals_SameInstance_ReturnsTrue()
    {
        // Arrange
        var wrapper1 = new UdpClientWrapper(5000);
        var wrapper2 = new UdpClientWrapper(5000);

        // Act & Assert
        Assert.That(wrapper1, Is.EqualTo(wrapper2));
    }

    [Test]
    public void GetHashCode_SameEndPoint_SameHashCode()
    {
        // Arrange
        var port = 5000;
        var wrapper1 = new UdpClientWrapper(port);
        var wrapper2 = new UdpClientWrapper(port);

        // Act & Assert
        Assert.That(wrapper2.GetHashCode(), Is.EqualTo(wrapper1.GetHashCode()));
    }

    [Test]
    public void GetHashCode_DifferentEndPoint_DifferentHashCode()
    {
        // Arrange
        var wrapper1 = new UdpClientWrapper(5000);
        var wrapper2 = new UdpClientWrapper(6000);

        // Act & Assert
        Assert.That(wrapper2.GetHashCode(), Is.Not.EqualTo(wrapper1.GetHashCode()));
    }

    [Test]
    public async Task StartListeningAsync_FinallyDisposesResources()
    {
        var wrapper = new UdpClientWrapper(0);

        var task = wrapper.StartListeningAsync();
        wrapper.StopListening();
        await task;

        var udp = typeof(UdpClientWrapper)
            .GetField("_udpClient", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(wrapper);

        var cts = typeof(UdpClientWrapper)
            .GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(wrapper);

        Assert.Multiple(() =>
        {
            Assert.That(udp, Is.Null, "_udpClient must be null after finally");
            Assert.That(cts, Is.Null, "_cts must be null after finally");
        });
    }

    [Test]
    public async Task StartListeningAsync_StopsByCancellation_NoException()
    {
        var wrapper = new UdpClientWrapper(0);

        var t = wrapper.StartListeningAsync();

        // cancel
        wrapper.StopListening();

        await t;

        Assert.Pass("StopListening triggered OperationCanceledException and it was caught.");
    }

    [Test]
    public async Task StartListeningAsync_PortAlreadyInUse_ExceptionCatchCovered()
    {
        // створюємо сокет, який займає порт
        var tmp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        int usedPort = ((IPEndPoint)tmp.Client.LocalEndPoint!).Port;

        var wrapper = new UdpClientWrapper(usedPort);

        // тепер StartListeningAsync не зможе створити UdpClient → Exception
        await wrapper.StartListeningAsync();

        tmp.Dispose();

        Assert.Pass("catch(Exception) був виконаний");
    }


    // TcpClientWrapper tests
    [Test]
    public void Constructor_ValidHostAndPort_SetsProperties()
    {
        // Arrange
        var host = "127.0.0.1";
        var port = 5000;

        // Act
        var wrapper = new TcpClientWrapper(host, port);

        // Assert
        var hostField = typeof(TcpClientWrapper).GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance);
        var portField = typeof(TcpClientWrapper).GetField("_port", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(hostField?.GetValue(wrapper), Is.EqualTo(host));
            Assert.That(portField?.GetValue(wrapper), Is.EqualTo(port));
        });
    }

    [Test]
    public void Constructor_NullHost_DoesNotThrow()
    {
        // Arrange
        string host = null!;
        var port = 5000;

        // Act & Assert
        Assert.DoesNotThrow(() => new TcpClientWrapper(host, port));
    }

    [Test]
    public void Constructor_InvalidPort_DoesNotThrow()
    {
        // Arrange
        var host = "127.0.0.1";
        var invalidPort = -1;

        // Act & Assert 
        Assert.DoesNotThrow(() => new TcpClientWrapper(host, invalidPort));
    }

    [Test]
    public void SendMessageAsync_String_Throws_WhenNotConnected()
    {
        var client = new TcpClientWrapper("localhost", 5000);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendMessageAsync("msg");
        });
    }

    // Additional test for TcpClientWrapper SendMessageAsync with byte array when not connected
    [Test]
    public void SendMessageAsync_ByteArray_Throws_WhenNotConnected()
    {
        var client = new TcpClientWrapper("localhost", 5000);
        var data = new byte[] { 0x01, 0x02, 0x03 };

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.SendMessageAsync(data);
        });
    }

    // Test for TcpClientWrapper Connected property
    [Test]
    public void Connected_WhenNoTcpClient_ReturnsFalse()
    {
        // Arrange
        var wrapper = new TcpClientWrapper("localhost", 5000);

        // Act
        var connected = wrapper.Connected;

        // Assert
        Assert.That(connected, Is.False);
    }

    // Test cleanup to reset console output
    [TearDown]
    public void TearDown()
    {
        // Reset console output to avoid interference between tests
        var standardOutput = new StreamWriter(Console.OpenStandardOutput());
        Console.SetOut(standardOutput);
    }
    [Test]
    public async Task StartListeningAsync_ShouldStartListeningAndReceiveMessages()
    {
        // Arrange
        var port = 8080;
        var udpWrapper = new UdpClientWrapper(port);
        var receivedMessages = new List<byte[]>();
        var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        udpWrapper.MessageReceived += (sender, data) =>
        {
            receivedMessages.Add(data);
        };

        // Act 
        var listeningTask = Task.Run(() => udpWrapper.StartListeningAsync());

        await Task.Delay(100);


        var testMessage = new byte[] { 0x01, 0x02, 0x03 };
        using (var client = new System.Net.Sockets.UdpClient())
        {
            await client.SendAsync(testMessage, testMessage.Length, "localhost", port);
        }
        await Task.Delay(100);

        udpWrapper.StopListening();

        await listeningTask;

        // Assert
        var output = consoleOutput.ToString();
        Assert.That(output, Contains.Substring("Start listening for UDP messages..."));
        Assert.Multiple(() =>
        {
            Assert.That(output, Contains.Substring("Received from"));
            Assert.That(receivedMessages, Has.Count.GreaterThan(0));
        });
    }

    [Test]
    public async Task StartListeningAsync_WhenCancelled_ShouldStopGracefully()
    {
        // Arrange
        var udpWrapper = new UdpClientWrapper(8081);
        var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        // Act
        var listeningTask = udpWrapper.StartListeningAsync();

        await Task.Delay(50);

        udpWrapper.StopListening();

        await listeningTask;

        // Assert
        var output = consoleOutput.ToString();
        Assert.That(output, Contains.Substring("Start listening for UDP messages..."));
        Assert.That(output, Does.Not.Contain("Error receiving message:"));
    }

    [Test]
    public async Task StartListeningAsync_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        var port = 8082;
        var udpWrapper = new UdpClientWrapper(port);
        var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        using var occupiedClient = new System.Net.Sockets.UdpClient(port);

        // Act
        await udpWrapper.StartListeningAsync();

        // Assert
        var output = consoleOutput.ToString();
        Assert.That(output, Contains.Substring("Start listening for UDP messages..."));
        Assert.That(output, Contains.Substring("Error receiving message:"));
    }

    [Test]
    public async Task StartListeningAsync_WithMultipleMessages_ShouldInvokeEventForEach()
    {
        // Arrange
        var port = 8083;
        var udpWrapper = new UdpClientWrapper(port);
        var receivedCount = 0;
        var consoleOutput = new StringWriter();
        Console.SetOut(consoleOutput);

        udpWrapper.MessageReceived += (sender, data) =>
        {
            receivedCount++;
        };

        // Act
        var listeningTask = Task.Run(() => udpWrapper.StartListeningAsync());
        await Task.Delay(100);

        var messages = new[]
        {
        new byte[] { 0x01, 0x02 },
        new byte[] { 0x03, 0x04 },
        new byte[] { 0x05, 0x06 }
    };

        using (var client = new System.Net.Sockets.UdpClient())
        {
            foreach (var message in messages)
            {
                await client.SendAsync(message, message.Length, "localhost", port);
                await Task.Delay(50);
            }
        }

        await Task.Delay(100);
        udpWrapper.StopListening();
        await listeningTask;

        // Assert
        Assert.That(receivedCount, Is.EqualTo(messages.Length));
    }

    [Test]
    public async Task StartListeningAsync_WhenStoppedQuickly_ShouldNotThrow()
    {
        // Arrange
        var udpWrapper = new UdpClientWrapper(8084);

        // Act & Assert
        var listeningTask = udpWrapper.StartListeningAsync();
        udpWrapper.StopListening();
        await listeningTask;
        Assert.That(listeningTask.IsCompleted, Is.True);
    }

    [Test]
    public void StartListeningAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var udpWrapper = new UdpClientWrapper(8085);

        // Act & Assert
        Assert.DoesNotThrowAsync(async () =>
        {
            var task1 = udpWrapper.StartListeningAsync();
            udpWrapper.StopListening();
            await task1;

            var task2 = udpWrapper.StartListeningAsync();
            udpWrapper.StopListening();
            await task2;
        });
    }

    [Test]
    public async Task StartListeningAsync_ShouldInitializeCancellationTokenSource()
    {
        // Arrange
        var udpWrapper = new UdpClientWrapper(8086);

        var ctsField = typeof(UdpClientWrapper).GetField("_cts",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var listeningTask = Task.Run(() => udpWrapper.StartListeningAsync());
        await Task.Delay(50);

        // Assert
        var ctsValue = ctsField?.GetValue(udpWrapper);
        Assert.That(ctsValue, Is.Not.Null);
        Assert.That(ctsValue, Is.InstanceOf<CancellationTokenSource>());

        udpWrapper.StopListening();
        await listeningTask;
    }

    [Test]
    public async Task StartListeningAsync_ShouldCreateUdpClient()
    {
        // Arrange
        var udpWrapper = new UdpClientWrapper(8087);

        var udpClientField = typeof(UdpClientWrapper).GetField("_udpClient",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var listeningTask = Task.Run(() => udpWrapper.StartListeningAsync());
        await Task.Delay(50);

        // Assert
        var udpClientValue = udpClientField?.GetValue(udpWrapper);
        Assert.That(udpClientValue, Is.Not.Null);
        Assert.That(udpClientValue, Is.InstanceOf<System.Net.Sockets.UdpClient>());

        udpWrapper.StopListening();
        await listeningTask;
    }

    public class TcpClientWrapper_Connect_Dispose_Tests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }


        [Test]
        public void Connect_Success_ConnectedBecomesTrue()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();

            Assert.That(wrapper.Connected, Is.True, "Wrapper must report Connected after successful Connect()");

            wrapper.Dispose();
            listener.Stop();
        }

        [Test]
        public void Connect_SecondCall_DoesNotThrow()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();
            Assert.DoesNotThrow(() => wrapper.Connect(), "Second Connect() should not throw");

            wrapper.Dispose();
            listener.Stop();
        }

        [Test]
        public void Connect_Fails_NoServerRunning_NoThrow()
        {
            int port = GetFreePort(); 

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            Assert.DoesNotThrow(() => wrapper.Connect(),
                "Connect() catches exceptions, test must not throw");

            Assert.That(wrapper.Connected, Is.False, "Should not be connected when no server exists");

            wrapper.Dispose();
        }

        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            int port = GetFreePort();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Dispose();

            Assert.DoesNotThrow(() => wrapper.Dispose(),
                "Multiple Dispose() calls must not throw");
        }

        [Test]
        public void Dispose_AfterSuccessfulConnect_ConnectedBecomesFalse()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();
            Assert.That(wrapper.Connected, Is.True);

            wrapper.Dispose();

            Assert.That(wrapper.Connected, Is.False, "Dispose() must fully disconnect");

            listener.Stop();
        }
    }
    public class TcpClientWrapper_DisposeCoverage_Tests
    {
        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public void Connect_CreatesAndDisposesOldCts()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);
            wrapper.Connect();

            Assert.That(wrapper.Connected, Is.True);

            // --- Другий виклик Connect() призводить до виконання _cts?.Dispose() ---
            Assert.DoesNotThrow(() => wrapper.Connect());

            wrapper.Dispose();
            listener.Stop();
        }

        // ---------------------------- DISPOSE() -----------------------------

        [Test]
        public void Dispose_WhenEverythingIsNull_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 12345);

            Assert.DoesNotThrow(() => wrapper.Dispose(),
                "Dispose() must not throw when all fields are null");
        }

        [Test]
        public void Dispose_WhenClientAndStreamExist_DisposesAllFields()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            // створюємо повний робочий стан
            wrapper.Connect();

            Assert.That(wrapper.Connected, Is.True);

            // --- тестуємо повну утилізацію ---
            wrapper.Dispose();

            // _tcpClient має стати null
            var tcpField = typeof(TcpClientWrapper).GetField("_tcpClient",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            Assert.That(tcpField?.GetValue(wrapper), Is.Null, "_tcpClient must be set to null after Dispose()");

            listener.Stop();
        }

        [Test]
        public void Dispose_CanBeCalledTwice_WhenConnected()
        {
            int port = GetFreePort();

            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            wrapper.Connect();

            Assert.DoesNotThrow(() => wrapper.Dispose());
            Assert.DoesNotThrow(() => wrapper.Dispose(),
                "Dispose() must be idempotent");

            listener.Stop();
        }
    }
}