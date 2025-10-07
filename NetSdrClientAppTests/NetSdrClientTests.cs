using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NetSdrClientApp.Messages;
using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    public class NetSdrClientTests
    {
        NetSdrClient _client;
        Mock<ITcpClient> _tcpMock;
        Mock<IUdpClient> _udpMock;

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
            await _client.ConnectAsync();

            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
        }

        [Test]
        public async Task ConnectAsync_WhenAlreadyConnected_DoesNotReconnect()
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);

            await _client.ConnectAsync();

            _tcpMock.Verify(tcp => tcp.Connect(), Times.Never);
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
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

            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
            Assert.That(_client.IQStarted, Is.True);
        }

        [Test]
        public async Task StopIQNoConnectionTest()
        {
            await _client.StopIQAsync();
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
            _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce);
        }

        [Test]
        public async Task StopIQTest()
        {
            await ConnectAsyncTest();
            await _client.StartIQAsync();
            await _client.StopIQAsync();

            _udpMock.Verify(udp => udp.StopListening(), Times.Once);
            Assert.That(_client.IQStarted, Is.False);
        }

        [Test]
        public async Task ChangeFrequencyAsync_SendsCorrectMessage()
        {
            await ConnectAsyncTest();
            await _client.ChangeFrequencyAsync(100000000, 0);

            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4));
        }

        [Test]
        public async Task ChangeFrequencyAsync_WithDifferentChannel_SendsMessage()
        {
            await ConnectAsyncTest();
            await _client.ChangeFrequencyAsync(144000000, 1);

            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4));
        }

        [Test]
        public async Task ChangeFrequencyAsync_MultipleTimes_SendsMultipleMessages()
        {
            await ConnectAsyncTest();

            await _client.ChangeFrequencyAsync(100000000, 0);
            await _client.ChangeFrequencyAsync(144000000, 0);
            await _client.ChangeFrequencyAsync(430000000, 1);

            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(6));
        }

        [Test]
        public async Task IQStarted_InitiallyFalse()
        {
            Assert.That(_client.IQStarted, Is.False);
        }

        [Test]
        public async Task MultipleStartStop_IQState_TogglesCorrectly()
        {
            await ConnectAsyncTest();

            await _client.StartIQAsync();
            Assert.That(_client.IQStarted, Is.True);

            await _client.StopIQAsync();
            Assert.That(_client.IQStarted, Is.False);

            await _client.StartIQAsync();
            Assert.That(_client.IQStarted, Is.True);

            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Exactly(2));
            _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        }

        [Test]
        public async Task FullWorkflow_ConnectStartChangeFrequencyStop()
        {
            await _client.ConnectAsync();
            Assert.That(_client.IQStarted, Is.False);

            await _client.StartIQAsync();
            Assert.That(_client.IQStarted, Is.True);

            await _client.ChangeFrequencyAsync(100000000, 0);
            await _client.ChangeFrequencyAsync(144000000, 1);

            await _client.StopIQAsync();
            Assert.That(_client.IQStarted, Is.False);

            _client.Disconect();

            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(7));
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
            _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        }

        // ===================== Додаткові тести для високого покриття =====================

        [Test]
        public async Task StartIQAsync_NoConnection_DoesNotStartIQ()
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

            await _client.StartIQAsync();

            Assert.That(_client.IQStarted, Is.False);
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Never);
        }

        [Test]
        public async Task StopIQAsync_NoConnection_DoesNotStopIQ()
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

            await _client.StopIQAsync();

            Assert.That(_client.IQStarted, Is.False);
            _udpMock.Verify(udp => udp.StopListening(), Times.Never);
        }

        [Test]
        public async Task ChangeFrequencyAsync_NoConnection_DoesNotSend()
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

            await _client.ChangeFrequencyAsync(100000000, 0);

            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        }

        [Test]
        public async Task ConnectAsync_WhenTcpThrowsException_DoesNotCrash()
        {
            _tcpMock.Setup(tcp => tcp.Connect()).Throws(new Exception("Connection failed"));

            Assert.DoesNotThrowAsync(async () => await _client.ConnectAsync());
            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
        }

        [Test]
        public async Task SendTcpRequest_ReceivesResponse()
        {
            _tcpMock.Setup(tcp => tcp.Connected).Returns(true);

            var msg = new byte[] { 0x01 };
            byte[] response = new byte[] { 0x02 };

            _tcpMock.Setup(tcp => tcp.SendMessageAsync(msg)).Callback(() =>
            {
                _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, response);
            });

            var method = typeof(NetSdrClient).GetMethod("SendTcpRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task<byte[]>)method.Invoke(_client, new object[] { msg });

            var result = await task;
            Assert.That(result, Is.EqualTo(response));
        }
    }
}
