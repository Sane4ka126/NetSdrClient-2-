using NetSdrClientApp.Messages;
using NUnit.Framework;
using System;
using System.Linq;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void GetControlItemMessageTest()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);
            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);
            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            // Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(actualCode, Is.EqualTo((short)code));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            // Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);
            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);
            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            // Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetControlItemMessage_ZeroParameters_ReturnsCorrectMessage()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            byte[] parameters = Array.Empty<byte>();

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);
            var headerBytes = msg.Take(2).ToArray();
            var codeBytes = msg.Skip(2).Take(2).ToArray();
            var num = BitConverter.ToUInt16(headerBytes);
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(4)); // Header (2) + Code (2)
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualLength, Is.EqualTo(4));
            Assert.That(actualCode, Is.EqualTo((short)code));
        }

        [Test]
        public void GetDataItemMessage_MaxLength_ParsesCorrectly()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem0;
            int parametersLength = 8192; // _maxDataItemMessageLength - header (2)

            // Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var parsedType, out var parsedCode, out var parsedSequenceNumber, out var parsedBody);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(msg.Length, Is.EqualTo(8194)); // Header (2) + Parameters (8192)
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(parsedSequenceNumber, Is.EqualTo(0));
            Assert.That(parsedBody.Length, Is.EqualTo(parametersLength));
        }

        [Test]
        public void TranslateMessage_ControlItem_ValidMessage_ParsesCorrectly()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            byte[] parameters = new byte[] { 0x01, 0x02, 0x03 };
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var parsedType, out var parsedCode, out var parsedSequenceNumber, out var parsedBody);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedCode, Is.EqualTo(code));
            Assert.That(parsedSequenceNumber, Is.EqualTo(0));
            Assert.That(parsedBody, Is.EquivalentTo(parameters));
        }

        [Test]
        public void TranslateMessage_DataItem_ValidMessage_ParsesCorrectly()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem1;
            byte[] parameters = new byte[] { 0x04, 0x05 };
            ushort sequenceNumber = 123;
            // Manually construct message: header (type + length), sequence number, parameters
            var header = BitConverter.GetBytes((ushort)(6 + ((int)type << 13))); // Length: header(2) + seq(2) + params(2)
            var seqBytes = BitConverter.GetBytes(sequenceNumber);
            var msg = header.Concat(seqBytes).Concat(parameters).ToArray();

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var parsedType, out var parsedCode, out var parsedSequenceNumber, out var parsedBody);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(parsedSequenceNumber, Is.EqualTo(sequenceNumber));
            Assert.That(parsedBody, Is.EquivalentTo(parameters));
        }

        [Test]
        public void TranslateMessage_InvalidControlItemCode_ReturnsFalse()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.ControlItemRange;
            var invalidCode = (ushort)0xFFFF; // Not defined in ControlItemCodes
            byte[] parameters = new byte[] { 0x01 };
            var header = BitConverter.GetBytes((ushort)(5 + ((int)type << 13))); // Length: header(2) + code(2) + params(1)
            var codeBytes = BitConverter.GetBytes(invalidCode);
            var msg = header.Concat(codeBytes).Concat(parameters).ToArray();

            // Act
            bool success = NetSdrMessageHelper.TranslateMessage(msg, out var parsedType, out var parsedCode, out var parsedSequenceNumber, out var parsedBody);

            // Assert
            Assert.That(success, Is.False);
            Assert.That(parsedType, Is.EqualTo(type));
            Assert.That(parsedCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(parsedSequenceNumber, Is.EqualTo(0));
            Assert.That(parsedBody, Is.EquivalentTo(parameters));
        }

        [Test]
        public void GetSamples_ValidSampleSize_ReturnsCorrectSamples()
        {
            // Arrange
            ushort sampleSize = 16; // 2 bytes per sample
            byte[] body = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Two samples: [0x0201, 0x0403]
            int[] expectedSamples = new int[] { BitConverter.ToInt32(new byte[] { 0x01, 0x02, 0x00, 0x00 }), 
                                               BitConverter.ToInt32(new byte[] { 0x03, 0x04, 0x00, 0x00 }) };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples, Is.EquivalentTo(expectedSamples));
        }

        [Test]
        public void GetSamples_InvalidSampleSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ushort sampleSize = 40; // Invalid: > 4 bytes
            byte[] body = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetControlItemMessage_ExceedsMaxMessageLength_ThrowsArgumentException()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            byte[] parameters = new byte[8190]; // Total length: header(2) + code(2) + params(8190) = 8194 > _maxMessageLength (8191)

            // Act & Assert
            Assert.Throws<ArgumentException>(() => NetSdrMessageHelper.GetControlItemMessage(type, code, parameters));
        }
    }
}
