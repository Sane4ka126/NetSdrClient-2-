using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    private NetSdrClient _client = null!;
    private Mock<ITcpClient> _tcpMock = null!;
    private Mock<IUdpClient> _udpMock = null!;

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
        _udpMock = new Mock<IUdpClient>();
        _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
    }

    [Test]
    public async Task ConnectAsyncTest()
    {
        //Act
        await _client.ConnectAsync();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task ConnectAsync_AlreadyConnected_DoesNotReconnect()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        
        //Act
        await _client.ConnectAsync();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public void DisconnectWithNoConnectionTest()
    {
        //Act
        _client.Disconect();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        //Arrange 
        await _client.ConnectAsync();
        
        //Act
        _client.Disconect();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        //Act
        await _client.StartIQAsync();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StartIQTest()
    {
        //Arrange 
        await _client.ConnectAsync();
        
        //Act
        await _client.StartIQAsync();
        
        //Assert
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQNoConnectionTest()
    {
        //Act
        await _client.StopIQAsync();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await _client.ConnectAsync();
        await _client.StartIQAsync();
        
        //Act
        await _client.StopIQAsync();
        
        //Assert
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithConnection_SendsMessage()
    {
        //Arrange
        await _client.ConnectAsync();
        long frequency = 14250000; // 14.25 MHz
        int channel = 0;
        
        //Act
        await _client.ChangeFrequencyAsync(frequency, channel);
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4)); // 3 from connect + 1 from frequency change
    }

    [Test]
    public async Task ChangeFrequencyAsync_NoConnection_DoesNotSendMessage()
    {
        //Arrange
        long frequency = 7100000; // 7.1 MHz
        int channel = 0;
        
        //Act
        await _client.ChangeFrequencyAsync(frequency, channel);
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task ChangeFrequencyAsync_DifferentChannels_SendsCorrectMessages()
    {
        //Arrange
        await _client.ConnectAsync();
        long frequency = 21000000; // 21 MHz
        
        //Act
        await _client.ChangeFrequencyAsync(frequency, 0);
        await _client.ChangeFrequencyAsync(frequency, 1);
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(5)); // 3 from connect + 2 frequency changes
    }

    [Test]
    public async Task StartStopIQ_Sequence_WorksCorrectly()
    {
        //Arrange
        await _client.ConnectAsync();
        
        //Act & Assert - Start
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        
        //Act & Assert - Stop
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
    }

    [Test]
    public async Task StartIQ_CalledTwice_StartsListeningTwice()
    {
        //Arrange
        await _client.ConnectAsync();
        
        //Act
        await _client.StartIQAsync();
        await _client.StartIQAsync();
        
        //Assert
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public void Constructor_SubscribesToEvents()
    {
        //Arrange & Act
        var tcpMock = new Mock<ITcpClient>();
        var udpMock = new Mock<IUdpClient>();
        var client = new NetSdrClient(tcpMock.Object, udpMock.Object);
        
        //Assert
        tcpMock.VerifyAdd(tcp => tcp.MessageReceived += It.IsAny<EventHandler<byte[]>>(), Times.Once);
        udpMock.VerifyAdd(udp => udp.MessageReceived += It.IsAny<EventHandler<byte[]>>(), Times.Once);
    }

    [Test]
    public async Task FullWorkflow_ConnectStartChangeFrequencyStop_WorksCorrectly()
    {
        //Arrange & Act
        await _client.ConnectAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        await _client.ChangeFrequencyAsync(14250000, 0);
        
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        _client.Disconect();
        
        //Assert
        Assert.Multiple(() =>
        {
            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(5));
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
            _udpMock.Verify(udp => udp.StopListening(), Times.Once);
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        });
    }
}
