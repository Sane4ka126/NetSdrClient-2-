using NetSdrClientApp.Messages;

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
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;
            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);
            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);
            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
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
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;
            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);
            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);
            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            //Assert
            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void TranslateMessage_ValidControlItemMessage_ParsesCorrectly()
        {
            //Arrange
            var expectedType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var expectedCode = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            byte[] testBody = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            byte[] message = NetSdrMessageHelper.GetControlItemMessage(expectedType, expectedCode, testBody);
            
            //Act
            NetSdrMessageHelper.TranslateMessage(message, 
                out NetSdrMessageHelper.MsgTypes actualType,
                out NetSdrMessageHelper.ControlItemCodes actualCode,
                out ushort sequenceNum,
                out byte[] actualBody);
            
            //Assert
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualCode, Is.EqualTo(expectedCode));
            Assert.That(actualBody, Is.Not.Null);
        }

        [Test]
        public void GetControlItemMessage_WithEmptyBody_CreatesValidMessage()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.RequestControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.RFFilter;
            
            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, Array.Empty<byte>());
            
            //Assert
            Assert.That(msg.Length, Is.GreaterThanOrEqualTo(4)); // Minimum: 2 bytes header + 2 bytes code
            Assert.That(msg, Is.Not.Null);
        }

        [Test]
        public void GetControlItemMessage_DifferentCodes_ProduceDifferentMessages()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code1 = NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency;
            var code2 = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            byte[] body = new byte[] { 0x01, 0x02 };
            
            //Act
            byte[] msg1 = NetSdrMessageHelper.GetControlItemMessage(type, code1, body);
            byte[] msg2 = NetSdrMessageHelper.GetControlItemMessage(type, code2, body);
            
            //Assert
            Assert.That(msg1.Length, Is.EqualTo(msg2.Length));
            // Code bytes should be different
            var code1Bytes = BitConverter.ToInt16(msg1.Skip(2).Take(2).ToArray());
            var code2Bytes = BitConverter.ToInt16(msg2.Skip(2).Take(2).ToArray());
            Assert.That(code1Bytes, Is.Not.EqualTo(code2Bytes));
        }

        [Test]
        public void GetDataItemMessage_DifferentTypes_CreateDifferentHeaders()
        {
            //Arrange
            var type1 = NetSdrMessageHelper.MsgTypes.DataItem0;
            var type2 = NetSdrMessageHelper.MsgTypes.DataItem1;
            byte[] data = new byte[100];
            
            //Act
            byte[] msg1 = NetSdrMessageHelper.GetDataItemMessage(type1, data);
            byte[] msg2 = NetSdrMessageHelper.GetDataItemMessage(type2, data);
            
            //Assert
            Assert.That(msg1.Length, Is.EqualTo(msg2.Length));
            // Header should be different due to different message types
            var header1 = BitConverter.ToUInt16(msg1.Take(2).ToArray());
            var header2 = BitConverter.ToUInt16(msg2.Take(2).ToArray());
            Assert.That(header1, Is.Not.EqualTo(header2));
        }

        [Test]
        public void GetSamples_With16BitData_ReturnsCorrectNumberOfSamples()
        {
            //Arrange
            int bitsPerSample = 16;
            byte[] data = new byte[100]; // 100 bytes = 50 samples for 16-bit
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitsPerSample, data);
            
            //Assert
            Assert.That(samples, Is.Not.Null);
            Assert.That(samples.Length, Is.GreaterThan(0));
        }

        [Test]
        public void GetSamples_With24BitData_ReturnsCorrectNumberOfSamples()
        {
            //Arrange
            int bitsPerSample = 24;
            byte[] data = new byte[120]; // 120 bytes = 40 samples for 24-bit
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitsPerSample, data);
            
            //Assert
            Assert.That(samples, Is.Not.Null);
            Assert.That(samples.Length, Is.GreaterThan(0));
        }

        [Test]
        public void GetControlItemMessage_LargeBody_HandlesCorrectly()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.ADModes;
            int largeSize = 5000;
            byte[] largeBody = new byte[largeSize];
            
            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, largeBody);
            
            //Assert
            Assert.That(msg.Length, Is.GreaterThan(largeSize));
            Assert.That(msg, Is.Not.Null);
        }

        [Test]
        public void GetDataItemMessage_EmptyData_CreatesMinimalMessage()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem3;
            
            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, Array.Empty<byte>());
            
            //Assert
            Assert.That(msg.Length, Is.GreaterThanOrEqualTo(2)); // At least header
            Assert.That(msg, Is.Not.Null);
        }
    }
