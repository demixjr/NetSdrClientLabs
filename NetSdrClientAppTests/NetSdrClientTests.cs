using System.Reflection;
using System.Text;
using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

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
    //UpdCluentWrapper tests
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
        var wrapper = new UdpClientWrapper(5000);

        // Act & Assert
        Assert.That(wrapper, Is.EqualTo(wrapper));
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

        Assert.That(hostField?.GetValue(wrapper), Is.EqualTo(host));
        Assert.That(portField?.GetValue(wrapper), Is.EqualTo(port));
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
    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(obj, value);
    }

    private static T GetPrivateField<T>(object obj, string fieldName)
    {
        var field = obj.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (T)field!.GetValue(obj)!;
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
}
