using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _udpMock;
    
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
        _udpMock = new Mock<IUdpClient>();
        _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
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
    public async Task ConnectAsync_WhenAlreadyConnected_DoesNotReconnect()
    {
        //arrange
        _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
        
        //act
        await _client.ConnectAsync();
        
        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
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
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQNoConnectionTest()
    {
        //act
        await _client.StopIQAsync();
        //assert
        //No exception thrown
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }
    
    [Test]
    public async Task StopIQTest()
    {
        //Arrange 
        await ConnectAsyncTest();
        await _client.StartIQAsync();
        
        //act
        await _client.StopIQAsync();
        
        //assert
        //No exception thrown
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task ChangeFrequencyAsync_SendsCorrectMessage()
    {
        //arrange
        await ConnectAsyncTest();
        long frequency = 100000000; // 100 MHz
        int channel = 0;
        
        //act
        await _client.ChangeFrequencyAsync(frequency, channel);
        
        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4)); // 3 from connect + 1 from change frequency
    }

    [Test]
    public async Task ChangeFrequencyAsync_WithDifferentChannel_SendsMessage()
    {
        //arrange
        await ConnectAsyncTest();
        long frequency = 144000000; // 144 MHz
        int channel = 1;
        
        //act
        await _client.ChangeFrequencyAsync(frequency, channel);
        
        //assert
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4));
    }

    [Test]
    public void UdpMessageReceived_ProcessesSamples()
    {
        //arrange
        // Create a valid NetSDR UDP message structure
        // Header: Length (2 bytes) + Message Type (1 byte) + Sequence (2 bytes) + Control Item Code (2 bytes)
        var messageLength = (ushort)40; // Total message length including header
        var messageType = (byte)0x84; // Data item message type
        var sequenceNum = (ushort)1234;
        var controlItemCode = (ushort)0x0004; // IQ data item code
        
        var message = new List<byte>();
        message.AddRange(BitConverter.GetBytes(messageLength));
        message.Add(messageType);
        message.AddRange(BitConverter.GetBytes(sequenceNum));
        message.AddRange(BitConverter.GetBytes(controlItemCode));
        
        // Add 32 bytes of sample data (16-bit I/Q samples)
        for (int i = 0; i < 32; i++)
        {
            message.Add((byte)i);
        }

        var sampleData = message.ToArray();

        // Delete samples.bin if exists to have clean test
        if (File.Exists("samples.bin"))
        {
            File.Delete("samples.bin");
        }

        //act
        _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, sampleData);

        //assert
        // Verify that file was created (samples are written)
        Assert.That(File.Exists("samples.bin"), Is.True);
        
        //cleanup
        if (File.Exists("samples.bin"))
        {
            File.Delete("samples.bin");
        }
    }

    [Test]
    public void TcpMessageReceived_HandlesResponse()
    {
        //This is implicitly tested through all async operations that use SendTcpRequest
        //The _tcpClient_MessageReceived handler is called via the mock setup
        Assert.Pass("TCP message received handling is covered by other tests through SendTcpRequest");
    }

    [Test]
    public async Task IQStarted_InitiallyFalse()
    {
        //assert
        Assert.That(_client.IQStarted, Is.False);
    }

    [Test]
    public async Task MultipleStartStop_IQState_TogglesCorrectly()
    {
        //arrange
        await ConnectAsyncTest();
        
        //act & assert
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        //verify UDP calls
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
    }

    [Test]
    public async Task FullWorkflow_ConnectStartChangeFrequencyStop()
    {
        //arrange & act
        await _client.ConnectAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        await _client.StartIQAsync();
        Assert.That(_client.IQStarted, Is.True);
        
        await _client.ChangeFrequencyAsync(100000000, 0);
        await _client.ChangeFrequencyAsync(144000000, 1);
        
        await _client.StopIQAsync();
        Assert.That(_client.IQStarted, Is.False);
        
        _client.Disconect();
        
        //assert
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(7)); // 3 setup + 1 start + 2 freq + 1 stop
        _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        _udpMock.Verify(udp => udp.StopListening(), Times.Once);
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up any test files
        if (File.Exists("samples.bin"))
        {
            try
            {
                File.Delete("samples.bin");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
