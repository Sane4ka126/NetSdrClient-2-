using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace NetSdrClientAppTests;

public class NetSdrClientTests
{
    NetSdrClient _client;
    Mock<ITcpClient> _tcpMock;
    Mock<IUdpClient> _updMock;

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
        await _client.ConnectAsync();
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Test]
    public async Task DisconnectWithNoConnectionTest()
    {
        _client.Disconect();
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task DisconnectTest()
    {
        await ConnectAsyncTest();
        _client.Disconect();
        _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
    }

    [Test]
    public async Task StartIQNoConnectionTest()
    {
        await _client.StartIQAsync();
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
    }

    [Test]
    public async Task StartIQTest()
    {
        await ConnectAsyncTest();
        await _client.StartIQAsync();
        _updMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
        Assert.That(_client.IQStarted, Is.True);
    }

    [Test]
    public async Task StopIQTest()
    {
        await ConnectAsyncTest();
        await _client.StopIQAsync();
        _updMock.Verify(tcp => tcp.StopListening(), Times.Once);
        Assert.That(_client.IQStarted, Is.False);
    }

    // Нові тести
    [Test]
    public async Task ConnectAsync_ThrowsException_WhenTcpClientFails()
    {
        _tcpMock.Setup(tcp => tcp.Connect()).Throws(new InvalidOperationException("Connection failed"));
        Assert.ThrowsAsync<InvalidOperationException>(async () => await _client.ConnectAsync());
        _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
    }

    [Test]
    public async Task SendMessageAsync_WhenConnected_SendsData()
    {
        await _client.ConnectAsync();
        byte[] testData = new byte[] { 0x01, 0x02, 0x03 };
        await _client.SendMessageAsync(testData);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(testData), Times.Once);
    }

    [Test]
    public async Task SendMessageAsync_WhenNotConnected_DoesNotSend()
    {
        byte[] testData = new byte[] { 0x01, 0x02, 0x03 };
        await _client.SendMessageAsync(testData);
        _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
    }

    [Test]
    public async Task MessageReceivedEvent_TriggersCorrectly()
    {
        bool eventTriggered = false;
        _client.MessageReceived += (sender, data) => { eventTriggered = true; };
        await _client.ConnectAsync();
        byte[] testData = new byte[] { 0x01, 0x02 };
        _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testData);
        Assert.That(eventTriggered, Is.True);
    }
}
