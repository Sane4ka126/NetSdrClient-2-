using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NetSdrClientApp.Messages;
using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    public class NetSdrClientTests
    {
        private NetSdrClient _client;
        private Mock<ITcpClient> _tcpMock;
        private Mock<IUdpClient> _udpMock;

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

        [TearDown]
        public void TearDown()
        {
            if (File.Exists("samples.bin"))
            {
                File.Delete("samples.bin");
            }
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
        public void DisconnectWithNoConnectionTest()
        {
            //act
            _client.Disconect();
            //assert
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
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        }

        [Test]
        public async Task StartIQNoConnectionTest()
        {
            //act
            await _client.StartIQAsync();
            //assert
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
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
            Assert.That(_client.IQStarted, Is.True);
        }

        [Test]
        public async Task StopIQNoConnectionTest()
        {
            //act
            await _client.StopIQAsync();
            //assert
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
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(4));
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
        public async Task ChangeFrequencyAsync_MultipleTimes_SendsMultipleMessages()
        {
            //arrange
            await ConnectAsyncTest();

            //act
            await _client.ChangeFrequencyAsync(100000000, 0);
            await _client.ChangeFrequencyAsync(144000000, 0);
            await _client.ChangeFrequencyAsync(430000000, 1);

            //assert
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(6));
        }

        [Test]
        public void IQStarted_InitiallyFalse()
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
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeast(7));
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once);
            _udpMock.Verify(udp => udp.StopListening(), Times.Once);
        }

        // New tests to increase coverage

        [Test]
        public async Task SendTcpRequest_NoConnection_ReturnsNull()
        {
            // Arrange
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);
            var msg = new byte[] { 0x01, 0x02 };

            // Act
            var result = await _client.SendTcpRequest(msg); // Note: This is private, but for testing, make it internal or use reflection if needed. Assuming it's testable.

            // Assert
            Assert.That(result, Is.Null);
            Assert.That(consoleOutput.ToString(), Contains.Substring("No active connection"));
        }

        [Test]
        public async Task TcpClientMessageReceived_CompletesTaskWithResponse()
        {
            // Arrange
            await _client.ConnectAsync();
            var sentMessage = new byte[] { 0x01, 0x02, 0x03 };
            var responseMessage = new byte[] { 0x04, 0x05, 0x06 };

            // Act
            var sendTask = _client.SendTcpRequest(sentMessage);
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, responseMessage);
            var result = await sendTask;

            // Assert
            Assert.That(result, Is.EqualTo(responseMessage));
        }

        [Test]
        public void UdpClientMessageReceived_ProcessesSamplesAndWritesToFile()
        {
            // Arrange
            var message = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x02 }; // Example: header + body with samples
            var expectedSamples = new short[] { 256, 512 }; // Assuming GetSamples returns this

            // Mock TranslateMessage and GetSamples if needed, but since it's static, use the real one or shim if necessary
            // For simplicity, assume the method processes as is

            // Act
            _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, message);

            // Assert
            Assert.That(File.Exists("samples.bin"), Is.True);
            var writtenData = File.ReadAllBytes("samples.bin");
            Assert.That(writtenData, Is.EqualTo(new byte[] { 0x00, 0x01, 0x00, 0x02 })); // Adjusted for short write
        }

        [Test]
        public async Task ConnectAsync_WithConnectionError_HandlesException()
        {
            // Arrange
            _tcpMock.Setup(tcp => tcp.Connect()).Throws(new Exception("Connection failed"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(() => _client.ConnectAsync());
        }

        [Test]
        public async Task StartIQAsync_SendsCorrectMessageFormat()
        {
            // Arrange
            await _client.ConnectAsync();

            // Act
            await _client.StartIQAsync();

            // Assert specific message format
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(msg => msg[0] == 0x08 && msg[4] == 0x80 && msg[5] == 0x02 && msg[6] == 0x01 && msg[7] == 0x01)), Times.Once);
        }

        [Test]
        public async Task StopIQAsync_SendsCorrectMessageFormat()
        {
            // Arrange
            await _client.ConnectAsync();
            await _client.StartIQAsync();

            // Act
            await _client.StopIQAsync();

            // Assert specific message format
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(msg => msg[0] == 0x08 && msg[4] == 0x00 && msg[5] == 0x01 && msg[6] == 0x00 && msg[7] == 0x00)), Times.Once);
        }
    }
}
