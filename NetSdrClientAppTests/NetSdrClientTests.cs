using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static NetSdrClientApp.Messages.NetSdrMessageHelper;

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

            _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>(bytes =>
            {
                _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
            });

            _udpMock = new Mock<IUdpClient>();

            _client = new NetSdrClient(_tcpMock.Object, _udpMock.Object);
        }

        [Test]
        public async Task ConnectAsyncTest()
        {
            // Act
            await _client.ConnectAsync();

            // Assert
            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once());
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
        }

        [Test]
        public void DisconnectWithNoConnectionTest()
        {
            // Act
            _client.Disconnect();

            // Assert
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once());
        }

        [Test]
        public async Task DisconnectTest()
        {
            // Arrange 
            await _client.ConnectAsync();

            // Act
            _client.Disconnect();

            // Assert
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once());
        }

        [Test]
        public async Task StartIQNoConnectionTest()
        {
            // Act
            await _client.StartIQAsync();

            // Assert
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never());
            _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce());
        }

        [Test]
        public async Task StartIQTest()
        {
            // Arrange 
            await _client.ConnectAsync();

            // Act
            await _client.StartIQAsync();

            // Assert
            _udpMock.Verify(udp => udp.StartListeningAsync(), Times.Once());
            Assert.That(_client.IQStarted, Is.True);
        }

        [Test]
        public async Task StopIQTest()
        {
            // Arrange 
            await _client.ConnectAsync();

            // Act
            await _client.StopIQAsync();

            // Assert
            _udpMock.Verify(udp => udp.StopListening(), Times.Once());
            Assert.That(_client.IQStarted, Is.False);
        }

        // Нові тести
        [Test]
        public async Task ChangeFrequencyAsync_Connected_SendsCorrectMessage()
        {
            // Arrange
            await _client.ConnectAsync();
            long hz = 1000000;
            int channel = 1;
            byte[] expectedArgs = new byte[] { (byte)channel }.Concat(BitConverter.GetBytes(hz).Take(5)).ToArray();
            byte[] expectedMessage = NetSdrMessageHelper.GetControlItemMessage(
                MsgTypes.SetControlItem,
                ControlItemCodes.ReceiverFrequency,
                expectedArgs);

            // Act
            await _client.ChangeFrequencyAsync(hz, channel);

            // Assert
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.Is<byte[]>(msg => msg.SequenceEqual(expectedMessage))), Times.Once());
            _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce());
        }

        [Test]
        public async Task ChangeFrequencyAsync_NoConnection_DoesNotSendMessage()
        {
            // Arrange
            _tcpMock.Setup(tcp => tcp.Connected).Returns(false);

            // Act
            await _client.ChangeFrequencyAsync(1000000, 1);

            // Assert
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.Never());
            _tcpMock.VerifyGet(tcp => tcp.Connected, Times.AtLeastOnce());
        }

        [Test]
        public async Task TcpClientMessageReceived_HandlesResponseCorrectly()
        {
            // Arrange
            byte[] testMessage = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            await _client.ConnectAsync(); // Викликаємо ConnectAsync, який використовує SendTcpRequest

            // Act
            _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, testMessage);

            // Assert
            Assert.Pass("No exception thrown for message handling.");
        }

        [Test]
        public async Task UdpClientMessageReceived_WritesSamplesToFile()
        {
            // Arrange
            byte[] testMessage = new byte[] { 0x01, 0x00, 0x02, 0x00 }; // 2 16-бітні зразки
            string filePath = "samples.bin";
            if (File.Exists(filePath)) File.Delete(filePath); // Очищаємо файл перед тестом

            // Act
            _udpMock.Raise(udp => udp.MessageReceived += null, _udpMock.Object, testMessage);

            // Assert
            Assert.That(File.Exists(filePath), Is.True);
            byte[] fileContent = File.ReadAllBytes(filePath);
            Assert.That(fileContent, Is.EqualTo(testMessage));
        }

        [Test]
        public void Constructor_ThrowsArgumentNullException_WhenTcpClientIsNull()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new NetSdrClient(null, _udpMock.Object));
        }
    }
}
