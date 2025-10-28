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
}
