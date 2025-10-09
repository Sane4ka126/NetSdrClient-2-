using System;
using System.Linq;
using NetSdrClientApp.Messages;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetSdrMessageHelperTests
    {
        [Test]
        public void GetControlItemMessage_ValidInput_ReturnsCorrectMessage()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 100;

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToUInt16(codeBytes.ToArray());

            // Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(actualCode, Is.EqualTo((ushort)code));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetControlItemMessage_NullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                NetSdrMessageHelper.GetControlItemMessage(type, code, null));
        }

        [Test]
        public void GetControlItemMessage_MaxLength_ReturnsCorrectMessage()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            int parametersLength = 8187; // Max: 8191 - 2 (header) - 2 (code)

            // Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            // Assert
            Assert.That(msg.Length, Is.EqualTo(8191));
        }

        [Test]
        public void GetControlItemMessage_ExceedsMaxLength_ThrowsArgumentException()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 8188; // Exceeds max

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]));
        }

        [Test]
        public void GetDataItemMessage_ValidInput_ReturnsCorrectMessage()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 100;

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
        public void GetDataItemMessage_NullParameters_ThrowsArgumentNullException()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem0;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                NetSdrMessageHelper.GetDataItemMessage(type, null));
        }

        [Test]
        public void GetDataItemMessage_MaxDataItemLength_ReturnsZeroLengthInHeader()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem3;
            int parametersLength = 8192; // Max: 8194 - 2 (header)

            // Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var num = BitConverter.ToUInt16(msg.Take(2).ToArray());
            var lengthPart = num - ((int)type << 13);

            // Assert
            Assert.That(lengthPart, Is.EqualTo(0)); // Should be 0 for max length
            Assert.That(msg.Length, Is.EqualTo(8194));
        }

        [Test]
        public void TranslateMessage_ValidControlItemMessage_ReturnsTrue()
        {
            // Arrange
            var expectedType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var expectedCode = NetSdrMessageHelper.ControlItemCodes.RFFilter;
            var expectedParams = new byte[] { 1, 2, 3, 4, 5 };
            var msg = NetSdrMessageHelper.GetControlItemMessage(expectedType, expectedCode, expectedParams);

            // Act
            bool result = NetSdrMessageHelper.TranslateMessage(
                msg, out var actualType, out var actualCode, out var sequenceNumber, out var body);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualCode, Is.EqualTo(expectedCode));
            Assert.That(sequenceNumber, Is.EqualTo(0));
            Assert.That(body, Is.EqualTo(expectedParams));
        }

        [Test]
        public void TranslateMessage_ValidDataItemMessage_ReturnsTrue()
        {
            // Arrange
            var expectedType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var expectedParams = new byte[] { 10, 20, 30, 40 };
            var msg = NetSdrMessageHelper.GetDataItemMessage(expectedType, expectedParams);
            
            // Add sequence number manually
            var msgList = msg.ToList();
            msgList.InsertRange(2, BitConverter.GetBytes((ushort)123));
            msg = msgList.ToArray();

            // Act
            bool result = NetSdrMessageHelper.TranslateMessage(
                msg, out var actualType, out var actualCode, out var sequenceNumber, out var body);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
            Assert.That(sequenceNumber, Is.EqualTo(123));
            Assert.That(body, Is.EqualTo(expectedParams));
        }

        [Test]
        public void TranslateMessage_NullMessage_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                NetSdrMessageHelper.TranslateMessage(
                    null, out _, out _, out _, out _));
        }

        [Test]
        public void TranslateMessage_MessageTooShort_ReturnsFalse()
        {
            // Arrange
            var msg = new byte[] { 1 }; // Only 1 byte

            // Act
            bool result = NetSdrMessageHelper.TranslateMessage(
                msg, out _, out _, out _, out var body);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(body, Is.Empty);
        }

        [Test]
        public void TranslateMessage_InvalidControlItemCode_ReturnsFalse()
        {
            // Arrange
            var msg = new byte[10];
            msg[0] = 0x04; // Length = 4
            msg[1] = 0x00; // Type = SetControlItem (0)
            msg[2] = 0xFF; // Invalid code
            msg[3] = 0xFF;

            // Act
            bool result = NetSdrMessageHelper.TranslateMessage(
                msg, out _, out var itemCode, out _, out _);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TranslateMessage_MaxDataItemLength_ReturnsCorrectLength()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem0;
            var parameters = new byte[8192];
            var msg = NetSdrMessageHelper.GetDataItemMessage(type, parameters);

            // Act
            bool result = NetSdrMessageHelper.TranslateMessage(
                msg, out _, out _, out _, out var body);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(body.Length, Is.EqualTo(8192));
        }

        [Test]
        public void GetSamples_8BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ushort sampleSize = 8;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(4));
            Assert.That(samples[0], Is.EqualTo(0x01));
            Assert.That(samples[1], Is.EqualTo(0x02));
            Assert.That(samples[2], Is.EqualTo(0x03));
            Assert.That(samples[3], Is.EqualTo(0x04));
        }

        [Test]
        public void GetSamples_16BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ushort sampleSize = 16;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x0201));
            Assert.That(samples[1], Is.EqualTo(0x0403));
        }

        [Test]
        public void GetSamples_24BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
            ushort sampleSize = 24;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(0x030201));
            Assert.That(samples[1], Is.EqualTo(0x060504));
        }

        [Test]
        public void GetSamples_32BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ushort sampleSize = 32;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(1));
            Assert.That(samples[0], Is.EqualTo(0x04030201));
        }

        [Test]
        public void GetSamples_NullBody_ThrowsArgumentNullException()
        {
            // Arrange
            ushort sampleSize = 16;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, null).ToArray());
        }

        [Test]
        public void GetSamples_InvalidSampleSizeZero_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var body = new byte[] { 1, 2, 3, 4 };
            ushort sampleSize = 0;

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray());
        }

        [Test]
        public void GetSamples_InvalidSampleSizeTooLarge_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var body = new byte[] { 1, 2, 3, 4 };
            ushort sampleSize = 40; // > 32 bits

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray());
        }

        [Test]
        public void GetSamples_IncompleteSample_IgnoresRemaining()
        {
            // Arrange
            var body = new byte[] { 0x01, 0x02, 0x03 }; // 3 bytes, but 16-bit samples
            ushort sampleSize = 16;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples.Length, Is.EqualTo(1)); // Only 1 complete sample
            Assert.That(samples[0], Is.EqualTo(0x0201));
        }

        [Test]
        public void GetSamples_EmptyBody_ReturnsEmpty()
        {
            // Arrange
            var body = Array.Empty<byte>();
            ushort sampleSize = 16;

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            // Assert
            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void RoundTrip_ControlItemMessage_PreservesData()
        {
            // Arrange
            var type = NetSdrMessageHelper.MsgTypes.ControlItemRange;
            var code = NetSdrMessageHelper.ControlItemCodes.ADModes;
            var parameters = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            // Act
            var msg = NetSdrMessageHelper.GetControlItemMessage(type, code, parameters);
            bool success = NetSdrMessageHelper.TranslateMessage(
                msg, out var actualType, out var actualCode, out _, out var actualBody);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(actualCode, Is.EqualTo(code));
            Assert.That(actualBody, Is.EqualTo(parameters));
        }

        [Test]
        public void AllMessageTypes_CanBeEncodedAndDecoded()
        {
            // Test all message types
            var controlTypes = new[]
            {
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.MsgTypes.CurrentControlItem,
                NetSdrMessageHelper.MsgTypes.ControlItemRange,
                NetSdrMessageHelper.MsgTypes.Ack
            };

            var dataTypes = new[]
            {
                NetSdrMessageHelper.MsgTypes.DataItem0,
                NetSdrMessageHelper.MsgTypes.DataItem1,
                NetSdrMessageHelper.MsgTypes.DataItem2,
                NetSdrMessageHelper.MsgTypes.DataItem3
            };

            foreach (var type in controlTypes)
            {
                var msg = NetSdrMessageHelper.GetControlItemMessage(
                    type, NetSdrMessageHelper.ControlItemCodes.ReceiverState, new byte[] { 1 });
                bool success = NetSdrMessageHelper.TranslateMessage(
                    msg, out var actualType, out _, out _, out _);

                Assert.That(success, Is.True);
                Assert.That(actualType, Is.EqualTo(type));
            }

            foreach (var type in dataTypes)
            {
                var msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[] { 1 });
                bool success = NetSdrMessageHelper.TranslateMessage(
                    msg, out var actualType, out _, out _, out _);

                Assert.That(success, Is.True);
                Assert.That(actualType, Is.EqualTo(type));
            }
        }
    }
}
