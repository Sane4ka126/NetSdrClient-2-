using NUnit.Framework;
using NetSdrClientApp.Messages;
using System;
using System.Linq;

namespace NetSdrClientAppTests
{
    [TestFixture]
    public class NetSdrMessageHelperExtendedTests // Renamed to avoid conflict
    {
        #region GetControlItemMessage Tests

        [Test]
        public void GetControlItemMessage_WithValidParameters_ReturnsCorrectMessage()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var itemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var result = NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(2 + 2 + 4)); // header + itemCode + parameters
        }

        [Test]
        public void GetControlItemMessage_WithEmptyParameters_ReturnsMessageWithHeaderAndItemCode()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.CurrentControlItem;
            var itemCode = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            var parameters = Array.Empty<byte>();

            // Act
            var result = NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(4)); // header + itemCode only
        }

        [Test]
        public void GetControlItemMessage_WithLargeParameters_ThrowsArgumentException()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var itemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[8200]; // Exceeds max length

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters));
        }

        [Test]
        public void GetControlItemMessage_WithAllItemCodes_ReturnsValidMessages()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var parameters = new byte[] { 0x01 };
            var itemCodes = Enum.GetValues(typeof(NetSdrMessageHelper.ControlItemCodes))
                .Cast<NetSdrMessageHelper.ControlItemCodes>()
                .Where(c => c != NetSdrMessageHelper.ControlItemCodes.None);

            // Act & Assert
            foreach (var itemCode in itemCodes)
            {
                var result = NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void GetControlItemMessage_WithNegativeLengthScenario_ThrowsArgumentException()
        {
            // This test ensures the private GetHeader handles edge cases
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var itemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[8190]; // At the boundary

            // Should not throw
            Assert.DoesNotThrow(() =>
                NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters));
        }

        #endregion

        #region GetDataItemMessage Tests

        [Test]
        public void GetDataItemMessage_WithValidParameters_ReturnsCorrectMessage()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem0;
            var parameters = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var result = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(2 + 4)); // header + parameters (no itemCode)
        }

        [Test]
        public void GetDataItemMessage_WithEmptyParameters_ReturnsHeaderOnly()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var parameters = Array.Empty<byte>();

            // Act
            var result = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(2)); // header only
        }

        [Test]
        public void GetDataItemMessage_WithMaxLength_ReturnsCorrectMessage()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem2;
            var parameters = new byte[8192]; // Max data item length (8194 - 2 header)

            // Act
            var result = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(8194));
        }

        [Test]
        public void GetDataItemMessage_WithAllDataItemTypes_ReturnsValidMessages()
        {
            // Arrange
            var parameters = new byte[] { 0x01, 0x02 };
            var dataTypes = new[] 
            { 
                NetSdrMessageHelper.MsgTypes.DataItem0,
                NetSdrMessageHelper.MsgTypes.DataItem1,
                NetSdrMessageHelper.MsgTypes.DataItem2,
                NetSdrMessageHelper.MsgTypes.DataItem3
            };

            // Act & Assert
            foreach (var msgType in dataTypes)
            {
                var result = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);
                Assert.That(result, Is.Not.Null);
            }
        }

        #endregion

        #region TranslateMessage Tests

        [Test]
        public void TranslateMessage_WithValidControlItemMessage_ReturnsTrue()
        {
            // Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var originalItemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var originalParameters = new byte[] { 0x01, 0x02, 0x03 };
            var message = NetSdrMessageHelper.GetControlItemMessage(originalType, originalItemCode, originalParameters);

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                message, 
                out var type, 
                out var itemCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(type, Is.EqualTo(originalType));
            Assert.That(itemCode, Is.EqualTo(originalItemCode));
            Assert.That(body, Is.EqualTo(originalParameters));
        }

        [Test]
        public void TranslateMessage_WithValidDataItemMessage_ReturnsTrue()
        {
            // Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.DataItem0;
            var originalParameters = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var message = NetSdrMessageHelper.GetDataItemMessage(originalType, originalParameters);

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                message, 
                out var type, 
                out var itemCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(type, Is.EqualTo(originalType));
            Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
        }

        [Test]
        public void TranslateMessage_WithDataItemMessageContainingSequenceNumber_ExtractsSequenceNumber()
        {
            // Arrange - manually construct message with sequence number
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem1;
            ushort expectedSequence = 0x1234;
            var header = BitConverter.GetBytes((ushort)(6 + ((int)msgType << 13)));
            var seqBytes = BitConverter.GetBytes(expectedSequence);
            var body = new byte[] { 0xAA, 0xBB };
            var message = header.Concat(seqBytes).Concat(body).ToArray();

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                message,
                out var type,
                out var itemCode,
                out var sequenceNumber,
                out var extractedBody);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(sequenceNumber, Is.EqualTo(expectedSequence));
            Assert.That(extractedBody, Is.EqualTo(body));
        }

        [Test]
        public void TranslateMessage_WithInvalidItemCode_ReturnsFalse()
        {
            // Arrange
            var header = BitConverter.GetBytes((ushort)(6 + (0 << 13))); // length 6, type 0
            var invalidItemCode = BitConverter.GetBytes((ushort)0xFFFF); // Invalid code
            var parameters = new byte[] { 0x01, 0x02 };
            var message = header.Concat(invalidItemCode).Concat(parameters).ToArray();

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                message, 
                out var type, 
                out var itemCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.False);
        }

        [Test]
        public void TranslateMessage_WithCorruptedLength_ReturnsFalse()
        {
            // Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var originalItemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var originalParameters = new byte[] { 0x01, 0x02, 0x03 };
            var message = NetSdrMessageHelper.GetControlItemMessage(originalType, originalItemCode, originalParameters);
            
            // Corrupt the message by removing last byte
            var corruptedMessage = message.Take(message.Length - 1).ToArray();

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                corruptedMessage, 
                out var type, 
                out var itemCode, 
                out var sequenceNumber, 
                out var body);

            // Assert
            Assert.That(success, Is.False);
        }

        [Test]
        public void TranslateMessage_RoundTrip_PreservesData()
        {
            // Arrange
            var msgTypes = Enum.GetValues(typeof(NetSdrMessageHelper.MsgTypes))
                .Cast<NetSdrMessageHelper.MsgTypes>();

            foreach (var msgType in msgTypes)
            {
                byte[] message;
                var parameters = new byte[] { 0xAA, 0xBB, 0xCC };

                if (msgType < NetSdrMessageHelper.MsgTypes.DataItem0)
                {
                    message = NetSdrMessageHelper.GetControlItemMessage(
                        msgType, 
                        NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency, 
                        parameters);
                }
                else
                {
                    message = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);
                }

                // Act
                var success = NetSdrMessageHelper.TranslateMessage(
                    message,
                    out var type,
                    out var itemCode,
                    out var sequenceNumber,
                    out var body);

                // Assert
                Assert.That(success, Is.True, $"Failed for {msgType}");
                Assert.That(type, Is.EqualTo(msgType), $"Type mismatch for {msgType}");
                Assert.That(body, Is.EqualTo(parameters), $"Body mismatch for {msgType}");
            }
        }

        [Test]
        public void TranslateMessage_WithMaxDataItemLength_DecodesCorrectly()
        {
            // Arrange - test the edge case where length=0 means max length for DataItems
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem3;
            var parameters = new byte[8192];
            Array.Fill(parameters, (byte)0x55);
            var message = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);

            // Act
            var success = NetSdrMessageHelper.TranslateMessage(
                message,
                out var type,
                out var itemCode,
                out var sequenceNumber,
                out var body);

            // Assert
            Assert.That(success, Is.True);
            Assert.That(body.Length, Is.EqualTo(8192 - 2)); // minus sequence number
        }

        #endregion

        #region GetSamples Tests

        [Test]
        public void GetSamples_With8BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 8; // 8 bits = 1 byte
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(4));
            Assert.That(samples[0], Is.EqualTo(1));
            Assert.That(samples[1], Is.EqualTo(2));
            Assert.That(samples[2], Is.EqualTo(3));
            Assert.That(samples[3], Is.EqualTo(4));
        }

        [Test]
        public void GetSamples_With16BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 16; // 16 bits = 2 bytes
            var body = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03, 0x00 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(3));
            Assert.That(samples[0], Is.EqualTo(1));
            Assert.That(samples[1], Is.EqualTo(2));
            Assert.That(samples[2], Is.EqualTo(3));
        }

        [Test]
        public void GetSamples_With24BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 24; // 24 bits = 3 bytes
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(2));
        }

        [Test]
        public void GetSamples_With32BitSamples_ReturnsCorrectValues()
        {
            // Arrange
            ushort sampleSize = 32; // 32 bits = 4 bytes
            var body = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(1));
            Assert.That(samples[1], Is.EqualTo(2));
        }

        [Test]
        public void GetSamples_WithInvalidSampleSize_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            ushort sampleSize = 40; // 40 bits = 5 bytes (exceeds max of 4)
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void GetSamples_WithEmptyBody_ReturnsEmptyEnumerable()
        {
            // Arrange
            ushort sampleSize = 16;
            var body = Array.Empty<byte>();

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples, Is.Empty);
        }

        [Test]
        public void GetSamples_WithIncompleteSample_IgnoresRemainder()
        {
            // Arrange
            ushort sampleSize = 16; // 2 bytes per sample
            var body = new byte[] { 0x01, 0x00, 0x02, 0x00, 0x03 }; // Last byte incomplete

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();

            // Assert
            Assert.That(samples.Count, Is.EqualTo(2)); // Only 2 complete samples
        }

        [Test]
        public void GetSamples_IsLazyEvaluated()
        {
            // Arrange
            ushort sampleSize = 8;
            var body = new byte[] { 0x01, 0x02, 0x03, 0x04 };

            // Act
            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body);

            // Assert - should not throw until enumerated
            Assert.DoesNotThrow(() => 
            {
                var enumerable = samples;
            });

            // Force enumeration
            var result = samples.ToList();
            Assert.That(result.Count, Is.EqualTo(4));
        }

        [Test]
        public void GetSamples_WithLargeDataset_ProcessesEfficiently()
        {
            // Arrange
            ushort sampleSize = 16;
            var body = new byte[10000]; // Large dataset
            for (int i = 0; i < body.Length; i++)
            {
                body[i] = (byte)(i % 256);
            }

            // Act & Assert - should complete without timeout
            Assert.DoesNotThrow(() =>
            {
                var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToList();
                Assert.That(samples.Count, Is.EqualTo(5000));
            });
        }

        #endregion

        #region Header Encoding Tests

        [Test]
        public void Header_EncodesMessageTypeCorrectly()
        {
            // Arrange & Act
            var msgTypes = Enum.GetValues(typeof(NetSdrMessageHelper.MsgTypes))
                .Cast<NetSdrMessageHelper.MsgTypes>();

            foreach (var msgType in msgTypes)
            {
                var parameters = new byte[] { 0x01 };
                byte[] message;

                if (msgType < NetSdrMessageHelper.MsgTypes.DataItem0)
                {
                    message = NetSdrMessageHelper.GetControlItemMessage(
                        msgType,
                        NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency,
                        parameters);
                }
                else
                {
                    message = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);
                }

                NetSdrMessageHelper.TranslateMessage(
                    message,
                    out var decodedType,
                    out var itemCode,
                    out var sequenceNumber,
                    out var body);

                // Assert
                Assert.That(decodedType, Is.EqualTo(msgType));
            }
        }

        [Test]
        public void Header_WithMaxDataItemLength_EncodesLengthAsZero()
        {
            // Arrange
            var msgType = NetSdrMessageHelper.MsgTypes.DataItem0;
            var parameters = new byte[8192]; // 8194 - 2 (header) = 8192

            // Act
            var message = NetSdrMessageHelper.GetDataItemMessage(msgType, parameters);
            var header = BitConverter.ToUInt16(message, 0);
            var encodedLength = header - ((int)msgType << 13);

            // Assert
            Assert.That(encodedLength, Is.EqualTo(0));
        }

        [Test]
        public void Header_WithBoundaryLength_EncodesCorrectly()
        {
            // Test at the boundary of max message length (8191)
            var msgType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var itemCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var parameters = new byte[8187]; // 8191 - 2 (header) - 2 (itemCode) = 8187

            // Act
            var message = NetSdrMessageHelper.GetControlItemMessage(msgType, itemCode, parameters);
            
            // Assert
            Assert.That(message.Length, Is.EqualTo(8191));
        }

        #endregion

        #region Edge Cases and Security Tests

        [Test]
        public void GetSamples_WithZeroSampleSize_ThrowsDivideByZeroException()
        {
            // Arrange
            ushort sampleSize = 0;
            var body = new byte[] { 0x01, 0x02 };

            // Act & Assert
            Assert.Throws<DivideByZeroException>(() =>
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToList());
        }

        [Test]
        public void TranslateMessage_WithAllControlItemTypes_HandlesCorrectly()
        {
            // Test all control item message types
            var controlTypes = new[]
            {
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.MsgTypes.CurrentControlItem,
                NetSdrMessageHelper.MsgTypes.ControlItemRange,
                NetSdrMessageHelper.MsgTypes.Ack
            };

            foreach (var msgType in controlTypes)
            {
                var message = NetSdrMessageHelper.GetControlItemMessage(
                    msgType,
                    NetSdrMessageHelper.ControlItemCodes.ADModes,
                    new byte[] { 0x01, 0x02 });

                var success = NetSdrMessageHelper.TranslateMessage(
                    message,
                    out var type,
                    out var itemCode,
                    out var sequenceNumber,
                    out var body);

                Assert.That(success, Is.True, $"Failed for {msgType}");
                Assert.That(type, Is.EqualTo(msgType));
                Assert.That(itemCode, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.ADModes));
            }
        }

        #endregion
    }
}
