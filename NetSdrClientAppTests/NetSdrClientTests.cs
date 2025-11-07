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

    // Helper метод для підготовки підключеного клієнта
    private async Task ArrangeConnectedClient()
    {
        await _client.ConnectAsync();
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
    public void DisconnectWithNoConnectionTest()
    {
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
        await ArrangeConnectedClient();
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
        await ArrangeConnectedClient();
        //act
        await _client.StopIQAsync();
        //assert
        //No exception thrown
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    // Новий тест: перевірка зміни частоти без підключення
    [Test]
    public async Task ChangeFrequencyNoConnectionTest()
    {
        //act & assert
        var exception = Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _client.ChangeFrequencyAsync(100000000, 1));
        
        Assert.That(exception.Message, Is.EqualTo("TCP client is not connected."));
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    // Виправлений тест: перевірка обробки UDP повідомлень з валідними даними
    [Test]
    public void UdpMessageReceivedTest()
    {
        //Arrange
        // Створюємо мінімальне валідне UDP повідомлення з заголовком
        // Header: 2 bytes length + 2 bytes message type + тіло
        var testData = new byte[] 
        { 
            0x08, 0x00,  // Length (8 bytes total)
            0x84, 0x00,  // Message type (UDP data message)
            0x01, 0x02, 0x03, 0x04  // Body data (4 bytes)
        };

        //act
        _updMock.Raise(udp => udp.MessageReceived += null, _updMock.Object, testData);

        //assert
        // Перевіряємо що подія була оброблена без винятків
        Assert.Pass("UDP message handled without exception");
    }
}
