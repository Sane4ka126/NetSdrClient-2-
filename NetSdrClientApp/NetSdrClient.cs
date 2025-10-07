using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetSdrClientExtendedTests
    {
        private Mock<ITcpClient> _mockTcpClient;
        private Mock<IUdpClient> _mockUdpClient;

        [SetUp]
        public void Setup()
        {
            _mockTcpClient = new Mock<ITcpClient>();
            _mockUdpClient = new Mock<IUdpClient>();
        }

        [TearDown]
        public void TearDown()
        {
            // Cleanup test file
            if (File.Exists("samples.bin"))
                File.Delete("samples.bin");
        }

        // Тест 1: ConnectAsync коли вже підключено - не надсилає повідомлення
        [Test]
        public async Task ConnectAsync_AlreadyConnected_DoesNotSendMessages()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ConnectAsync();

            // Assert
            _mockTcpClient.Verify(x => x.Connect(), Times.Never);
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        }

        // Тест 2: ConnectAsync надсилає три повідомлення налаштування
        [Test]
        public async Task ConnectAsync_NotConnected_SendsThreeSetupMessages()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(false);

            int messageCount = 0;
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback(() => 
                {
                    messageCount++;
                    _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01, 0x02, 0x03 });
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ConnectAsync();

            // Assert
            Assert.That(messageCount, Is.EqualTo(3));
            _mockTcpClient.Verify(x => x.Connect(), Times.Once);
        }

        // Тест 3: StartIQAsync встановлює IQStarted і запускає UDP
        [Test]
        public async Task StartIQAsync_Connected_SetsIQStartedAndStartsUdp()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);
            _mockUdpClient.Setup(x => x.StartListeningAsync()).Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            var sendTask = client.StartIQAsync();
            _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01, 0x02, 0x03 });
            await sendTask;

            // Assert
            Assert.That(client.IQStarted, Is.True);
            _mockUdpClient.Verify(x => x.StartListeningAsync(), Times.Once);
        }

        // Тест 4: StartIQAsync без з'єднання не надсилає повідомлення
        [Test]
        public async Task StartIQAsync_NoConnection_DoesNothing()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(false);
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.StartIQAsync();

            // Assert
            Assert.That(client.IQStarted, Is.False);
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
            _mockUdpClient.Verify(x => x.StartListeningAsync(), Times.Never);
        }

        // Тест 5: StopIQAsync зупиняє IQ і UDP
        [Test]
        public async Task StopIQAsync_Connected_StopsIQAndUdp()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);
            client.IQStarted = true;

            // Act
            var sendTask = client.StopIQAsync();
            _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01, 0x02, 0x03 });
            await sendTask;

            // Assert
            Assert.That(client.IQStarted, Is.False);
            _mockUdpClient.Verify(x => x.StopListening(), Times.Once);
        }

        // Тест 6: StopIQAsync без з'єднання
        [Test]
        public async Task StopIQAsync_NoConnection_DoesNotStopUdp()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(false);
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.StopIQAsync();

            // Assert
            _mockUdpClient.Verify(x => x.StopListening(), Times.Never);
        }

        // Тест 7: ChangeFrequencyAsync надсилає правильне повідомлення
        [Test]
        public async Task ChangeFrequencyAsync_Connected_SendsMessage()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);

            byte[] sentMessage = null;
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg => 
                {
                    sentMessage = msg;
                    _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01, 0x02 });
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ChangeFrequencyAsync(14250000, 0);

            // Assert
            Assert.That(sentMessage, Is.Not.Null);
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        // Тест 8: ChangeFrequencyAsync без з'єднання
        [Test]
        public async Task ChangeFrequencyAsync_NoConnection_DoesNotSend()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(false);
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ChangeFrequencyAsync(14250000, 0);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        }

        // Тест 9: Disconnect викликає метод TCP клієнта
        [Test]
        public void Disconnect_CallsTcpClientDisconnect()
        {
            // Arrange
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            client.Disconect();

            // Assert
            _mockTcpClient.Verify(x => x.Disconnect(), Times.Once);
        }

        // Тест 10: TCP MessageReceived обробляє відповідь
        [Test]
        public async Task TcpMessageReceived_CompletesResponseTask()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);

            var responseBytes = new byte[] { 0xAA, 0xBB, 0xCC };
            
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg =>
                {
                    Task.Run(() => _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, responseBytes));
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ChangeFrequencyAsync(100000, 0);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        // Тест 11: UDP MessageReceived обробляє дані
        [Test]
        public void UdpMessageReceived_ProcessesSamples_CreatesFile()
        {
            // Arrange
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            var testData = new byte[] 
            { 
                0x04, 0x84, // Header
                0x00, 0x00, // Sequence
                0x01, 0x00, 0x02, 0x00, // Sample data
                0x03, 0x00, 0x04, 0x00
            };

            // Act
            _mockUdpClient.Raise(x => x.MessageReceived += null, _mockUdpClient.Object, testData);

            // Assert
            Assert.That(File.Exists("samples.bin"), Is.True);
        }

        // Тест 12: ChangeFrequencyAsync з різними каналами
        [Test]
        public async Task ChangeFrequencyAsync_DifferentChannel_SendsCorrectChannel()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);

            byte[] sentMessage = null;
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg => 
                {
                    sentMessage = msg;
                    _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01 });
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ChangeFrequencyAsync(7100000, 1);

            // Assert
            Assert.That(sentMessage, Is.Not.Null);
            Assert.That(sentMessage, Does.Contain((byte)1));
        }

        // Тест 13: IQStarted властивість за замовчуванням false
        [Test]
        public void IQStarted_DefaultValue_IsFalse()
        {
            // Arrange & Act
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Assert
            Assert.That(client.IQStarted, Is.False);
        }

        // Тест 14: Множинні виклики StartIQAsync
        [Test]
        public async Task StartIQAsync_MultipleCalls_WorksCorrectly()
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);

            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg =>
                {
                    _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01 });
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.StartIQAsync();
            await client.StopIQAsync();
            await client.StartIQAsync();

            // Assert
            Assert.That(client.IQStarted, Is.True);
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Exactly(3));
        }

        // Тест 15: ChangeFrequencyAsync з різними частотами
        [Test]
        [TestCase(7100000L, 0)]
        [TestCase(14250000L, 0)]
        [TestCase(21000000L, 1)]
        [TestCase(28500000L, 1)]
        public async Task ChangeFrequencyAsync_VariousFrequencies_SendsMessages(long frequency, int channel)
        {
            // Arrange
            _mockTcpClient.Setup(x => x.Connected).Returns(true);
            _mockTcpClient.Setup(x => x.SendMessageAsync(It.IsAny<byte[]>()))
                .Callback<byte[]>(msg =>
                {
                    _mockTcpClient.Raise(x => x.MessageReceived += null, _mockTcpClient.Object, new byte[] { 0x01 });
                })
                .Returns(Task.CompletedTask);

            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            await client.ChangeFrequencyAsync(frequency, channel);

            // Assert
            _mockTcpClient.Verify(x => x.SendMessageAsync(It.IsAny<byte[]>()), Times.Once);
        }

        // Тест 16: IQStarted можна встановити вручну
        [Test]
        public void IQStarted_CanBeSetManually()
        {
            // Arrange
            var client = new NetSdrClient(_mockTcpClient.Object, _mockUdpClient.Object);

            // Act
            client.IQStarted = true;

            // Assert
            Assert.That(client.IQStarted, Is.True);

            // Act
            client.IQStarted = false;

            // Assert
            Assert.That(client.IQStarted, Is.False);
        }
    }
}
