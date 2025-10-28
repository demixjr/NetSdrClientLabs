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
    public async Task ChangeFrequencyAsync_ReturnsCorrectMessage()
    {
        //Arrange
        await _client.ConnectAsync(); 
        long frequency = 145000000;
        int channel = 1;
        // Act
        await _client.ChangeFrequencyAsync(frequency, channel); 
        // Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()));
        _tcpMock.VerifyGet(tcp => tcp.Connected);
    }
    [Test]
    public async Task SendTcpRequest_NoConnection_ReturnsNull()
    { 
        // Arrange
        var message = new byte[] { 0x01, 0x02 }; 
        var method = typeof(NetSdrClient) .GetMethod("SendTcpRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        // Act
        var taskObj = method?.Invoke(_client, new object[] { message }); 
        var task = (Task<byte[]>)taskObj!;
        var response = await task;
        // Assert
        Assert.IsNull(response); 
    }
    [Test]
    public async Task TcpClient_MessageReceived_CompletesTask() 
    { 
        // Arrange
        _tcpMock = new Mock<ITcpClient>();
        _tcpMock.Setup(t => t.Connected).Returns(true); 

        _updMock = new Mock<IUdpClient>(); _client = new NetSdrClient(_tcpMock.Object, _updMock.Object); 

        var msg = new byte[] { 0x10, 0x20 };
        var response = new byte[] { 0xAB, 0xCD };
        var method = typeof(NetSdrClient) .GetMethod("SendTcpRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        // Act
        var taskObj = method?.Invoke(_client, new object[] { msg }); 
        var task = (Task<byte[]>)taskObj!; 
        _tcpMock.Raise(t => t.MessageReceived += null, _tcpMock.Object, response); 
        var result = await task;

        // Assert
        Assert.That(result, Is.EqualTo(response)); 
    } 
    [Test] 
    public void TcpClient_MessageReceived_NoPendingRequest_DoesNotThrow() 
    {
        // Arrange
        var msg = new byte[] { 0xAA, 0xBB }; 
        // Act & Assert
        Assert.DoesNotThrow(() => _tcpMock.Raise(t => t.MessageReceived += null, _tcpMock.Object, msg) );
    }
    }
