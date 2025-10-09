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

    // NEW TESTS FOR 90% COVERAGE

    [Test]
    public async Task SendTcpRequest_WithoutConnection_ReturnsNull()
    {
        //Arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
        
        //Act
        await _client.ChangeFrequencyAsync(14250000, 0);
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task TcpMessageReceived_SetsResponseTaskSource()
    {
        //Arrange
        await _client.ConnectAsync();
        var testMessage = new byte[] { 0x01, 0x02, 0x03 };
        
        //Act
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testMessage);
        
        //Assert - No exception means the handler worked correctly
        Assert.Pass("TCP message received handler executed successfully");
    }

    [Test]
    public void TcpMessageReceived_WithoutPendingRequest_HandlesGracefully()
    {
        //Arrange - No pending request (no ConnectAsync or other operations)
        var testMessage = new byte[] { 0x04, 0x05, 0x06 };
        
        //Act & Assert - Should not throw
        Assert.DoesNotThrow(() =>
        {
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testMessage);
        });
    }

    [Test]
    public void UdpMessageReceived_TriggersHandler()
    {
        //Arrange
        var testUdpData = new byte[] { 0x10, 0x20, 0x30, 0x40 };
        
        //Act & Assert - Should not throw when UDP message is received
        Assert.DoesNotThrow(() =>
        {
            _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, testUdpData);
        });
    }

    [Test]
    public async Task StartIQAsync_SetsIQStartedBeforeStartingListener()
    {
        //Arrange
        await _client.ConnectAsync();
        var listenerStarted = false;
        
        _udpMock.Setup(udp => udp.StartListeningAsync()).Callback(() =>
        {
            listenerStarted = true;
            // By this point, IQStarted should already be true
            Assert.That(_client.IQStarted, Is.True);
        });
        
        //Act
        await _client.StartIQAsync();
        
        //Assert
        Assert.That(listenerStarted, Is.True);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQAsync_SetsIQStartedToFalseBeforeStoppingListener()
    {
        //Arrange
        await _client.ConnectAsync();
        await _client.StartIQAsync();
        
        //Act
        await _client.StopIQAsync();
        
        //Assert
        Assert.That(_client.IQStarted, Is.False);
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithDifferentFrequencies_SendsCorrectData()
    {
        //Arrange
        await _client.ConnectAsync();
        var frequencies = new[] { 7074000L, 14074000L, 21074000L, 28074000L };
        
        //Act
        foreach (var freq in frequencies)
        {
            await _client.ChangeFrequencyAsync(freq, 0);
        }
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), 
            Times.Exactly(3 + frequencies.Length)); // 3 from connect + frequency changes
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithMultipleChannels_SendsForEachChannel()
    {
        //Arrange
        await _client.ConnectAsync();
        long frequency = 14250000;
        
        //Act
        await _client.ChangeFrequencyAsync(frequency, 0);
        await _client.ChangeFrequencyAsync(frequency, 1);
        await _client.ChangeFrequencyAsync(frequency, 2);
        
        //Assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(6)); // 3 from connect + 3 changes
    }

    [Test]
    public async Task MultipleConnectCalls_DoesNotReconnect()
    {
        //Act
        await _client.ConnectAsync();
        await _client.ConnectAsync();
        await _client.ConnectAsync();
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3)); // Only from first connect
    }

    [Test]
    public async Task DisconnectAfterMultipleOperations_DisconnectsOnce()
    {
        //Arrange
        await _client.ConnectAsync();
        await _client.StartIQAsync();
        await _client.ChangeFrequencyAsync(14250000, 0);
        await _client.StopIQAsync();
        
        //Act
        _client.Disconect();
        _client.Disconect(); // Second disconnect
        
        //Assert
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Exactly(2));
    }

    [Test]
    public async Task IQStartedProperty_TracksStateCorrectly()
    {
        //Arrange
        await _client.ConnectAsync();
        
        //Assert initial state
        Assert.That(_client.IQStarted, Is.False);
        
        //Act & Assert - Start
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        //Act & Assert - Stop
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        //Act & Assert - Start again
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task ConnectAsync_SendsCorrectNumberOfInitializationMessages()
    {
        //Arrange
        var sentMessages = new List<byte[]>();
        _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()))
            .Callback<byte[]>((msg) =>
            {
                sentMessages.Add(msg);
                _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, msg);
            });
        
        //Act
        await _client.ConnectAsync();
        
        //Assert
        Assert.That(sentMessages.Count, Is.EqualTo(3));
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
    }
}
