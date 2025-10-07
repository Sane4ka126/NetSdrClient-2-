using NetSdrClientApp.Messages;
using NUnit.Framework;
using System;
using System.Linq;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = MsgTypes.Ack;
            var code = ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(actualCode, Is.EqualTo((short)code));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));

            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        // New tests to increase coverage

        [Test]
        public void TranslateMessage_ValidMessage_ReturnsCorrectValues()
        {
            // Arrange
            var message = new byte[] { 0x20, 0x00, 0x00, 0x01, 0x00, 0x02 }; // Example: type Ack, code ReceiverState, seq 0, body [0x00, 0x02]
            MsgTypes expectedType = MsgTypes.Ack;
            ControlItemCodes expectedCode = ControlItemCodes.ReceiverState;
            ushort expectedSequenceNum = 0;
            var expectedBody = new byte[] { 0x00, 0x02 };

            // Act
            NetSdrMessageHelper.TranslateMessage(message, out var actualType, out var actualCode, out var actualSequenceNum, out var actualBody);

            // Assert
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualCode, Is.EqualTo(expectedCode));
            Assert.That(actualSequenceNum, Is.EqualTo(expectedSequenceNum));
            Assert.That(actualBody, Is.EqualTo(expectedBody));
        }

        [Test]
        public void TranslateMessage_InvalidMessage_ThrowsException()
        {
            // Arrange
            var invalidMessage = new byte[] { 0x01 }; // Too short

            // Act & Assert
            Assert.Throws<ArgumentException>(() => NetSdrMessageHelper.TranslateMessage(invalidMessage, out _, out _, out _, out _));
        }

        [Test]
        public void GetSamples_16Bit_ReturnsCorrectSamples()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x01, 0x00, 0x02 }; // Little-endian: 0x0100 = 256, 0x0200 = 512
            var expectedSamples = new short[] { 256, 512 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(16, data);

            // Assert
            Assert.That(samples, Is.EqualTo(expectedSamples));
        }

        [Test]
        public void GetSamples_InvalidBitDepth_ThrowsException()
        {
            // Arrange
            var data = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.Throws<ArgumentException>(() => NetSdrMessageHelper.GetSamples(8, data)); // Assuming only 16 supported
        }

        [Test]
        public void GetControlItemMessage_EmptyParameters_ReturnsMinimalMessage()
        {
            // Arrange
            var type = MsgTypes.SetControlItem;
            var code = ControlItemCodes.ReceiverState;
            var parameters = Array.Empty<byte>();

            // Act
            var msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(4)); // Header 2 + Code 2
            var header = BitConverter.ToUInt16(msg.Take(2).ToArray());
            var actualType = (MsgTypes)(header >> 13);
            Assert.That(actualType, Is.EqualTo(type));
            var actualCode = BitConverter.ToInt16(msg.Skip(2).Take(2).ToArray());
            Assert.That(actualCode, Is.EqualTo((short)code));
        }

        [Test]
        public void GetDataItemMessage_EmptyParameters_ReturnsMinimalMessage()
        {
            // Arrange
            var type = MsgTypes.DataItem2;
            var parameters = Array.Empty<byte>();

            // Act
            var msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(2)); // Header only
            var header = BitConverter.ToUInt16(msg.Take(2).ToArray());
            var actualType = (MsgTypes)(header >> 13);
            Assert.That(actualType, Is.EqualTo(type));
        }
    }
}
