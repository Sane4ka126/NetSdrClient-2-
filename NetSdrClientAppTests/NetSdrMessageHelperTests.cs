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
        public void GetControlItemMessage_WithEmptyParameters_CreatesValidMessage()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var code = NetSdrMessageHelper.ControlItemCodes.RFFilter;
            
            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[0]);
            
            //Assert
            Assert.That(msg.Length, Is.GreaterThanOrEqualTo(4)); // Header + Code minimum
            var num = BitConverter.ToUInt16(msg.Take(2).ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            Assert.That(actualType, Is.EqualTo(type));
        }

        [Test]
        public void GetControlItemMessage_WithDifferentTypes_CreatesCorrectMessages()
        {
            //Arrange & Act
            var setMsg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.SetControlItem, 
                NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency, 
                new byte[5]);
            
            var requestMsg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.RequestControlItem, 
                NetSdrMessageHelper.ControlItemCodes.ReceiverState, 
                new byte[0]);
            
            //Assert
            var setType = (NetSdrMessageHelper.MsgTypes)(BitConverter.ToUInt16(setMsg.Take(2).ToArray()) >> 13);
            var requestType = (NetSdrMessageHelper.MsgTypes)(BitConverter.ToUInt16(requestMsg.Take(2).ToArray()) >> 13);
            
            Assert.That(setType, Is.EqualTo(NetSdrMessageHelper.MsgTypes.SetControlItem));
            Assert.That(requestType, Is.EqualTo(NetSdrMessageHelper.MsgTypes.RequestControlItem));
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
        public void GetDataItemMessage_WithEmptyData_CreatesValidMessage()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem1;
            
            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[0]);
            
            //Assert
            Assert.That(msg.Length, Is.GreaterThanOrEqualTo(2)); // At least header
            var num = BitConverter.ToUInt16(msg.Take(2).ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            Assert.That(actualType, Is.EqualTo(type));
        }

        [Test]
        public void GetDataItemMessage_WithDifferentDataItemTypes_CreatesCorrectMessages()
        {
            //Arrange & Act
            var dataItem1 = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem1, 
                new byte[100]);
            
            var dataItem2 = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem2, 
                new byte[200]);
            
            var dataItem3 = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem3, 
                new byte[300]);
            
            //Assert
            var type1 = (NetSdrMessageHelper.MsgTypes)(BitConverter.ToUInt16(dataItem1.Take(2).ToArray()) >> 13);
            var type2 = (NetSdrMessageHelper.MsgTypes)(BitConverter.ToUInt16(dataItem2.Take(2).ToArray()) >> 13);
            var type3 = (NetSdrMessageHelper.MsgTypes)(BitConverter.ToUInt16(dataItem3.Take(2).ToArray()) >> 13);
            
            Assert.That(type1, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem1));
            Assert.That(type2, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem2));
            Assert.That(type3, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem3));
            Assert.That(dataItem1.Length, Is.EqualTo(102)); // 2 header + 100 data
            Assert.That(dataItem2.Length, Is.EqualTo(202)); // 2 header + 200 data
            Assert.That(dataItem3.Length, Is.EqualTo(302)); // 2 header + 300 data
        }

        [Test]
        public void TranslateMessage_WithValidControlItemMessage_ExtractsCorrectData()
        {
            //Arrange
            var expectedType = NetSdrMessageHelper.MsgTypes.Ack;
            var expectedCode = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            var expectedBody = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var message = NetSdrMessageHelper.GetControlItemMessage(expectedType, expectedCode, expectedBody);
            
            //Act
            NetSdrMessageHelper.TranslateMessage(
                message, 
                out NetSdrMessageHelper.MsgTypes actualType, 
                out NetSdrMessageHelper.ControlItemCodes actualCode, 
                out ushort sequenceNum, 
                out byte[] actualBody);
            
            //Assert
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualCode, Is.EqualTo(expectedCode));
            Assert.That(actualBody, Is.EqualTo(expectedBody));
        }

        [Test]
        public void TranslateMessage_WithValidDataItemMessage_ExtractsCorrectData()
        {
            //Arrange
            var expectedType = NetSdrMessageHelper.MsgTypes.DataItem1;
            var expectedBody = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
            var message = NetSdrMessageHelper.GetDataItemMessage(expectedType, expectedBody);
            
            //Act
            NetSdrMessageHelper.TranslateMessage(
                message, 
                out NetSdrMessageHelper.MsgTypes actualType, 
                out NetSdrMessageHelper.ControlItemCodes actualCode, 
                out ushort sequenceNum, 
                out byte[] actualBody);
            
            //Assert
            Assert.That(actualType, Is.EqualTo(expectedType));
            Assert.That(actualBody, Is.EqualTo(expectedBody));
        }

        [Test]
        public void GetSamples_With16BitMode_ExtractsCorrectSamples()
        {
            //Arrange
            int bitDepth = 16;
            var body = new byte[] 
            { 
                0x00, 0x01, // Sample 1: 256
                0x00, 0x02, // Sample 2: 512
                0x00, 0x03, // Sample 3: 768
                0x00, 0x04  // Sample 4: 1024
            };
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitDepth, body);
            
            //Assert
            Assert.That(samples.Length, Is.EqualTo(4));
            Assert.That(samples[0], Is.EqualTo(256));
            Assert.That(samples[1], Is.EqualTo(512));
            Assert.That(samples[2], Is.EqualTo(768));
            Assert.That(samples[3], Is.EqualTo(1024));
        }

        [Test]
        public void GetSamples_With24BitMode_ExtractsCorrectSamples()
        {
            //Arrange
            int bitDepth = 24;
            var body = new byte[] 
            { 
                0x00, 0x00, 0x01, // Sample 1: 65536
                0x00, 0x00, 0x02  // Sample 2: 131072
            };
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitDepth, body);
            
            //Assert
            Assert.That(samples.Length, Is.EqualTo(2));
            Assert.That(samples[0], Is.EqualTo(65536));
            Assert.That(samples[1], Is.EqualTo(131072));
        }

        [Test]
        public void GetSamples_WithEmptyBody_ReturnsEmptyArray()
        {
            //Arrange
            int bitDepth = 16;
            var body = new byte[0];
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitDepth, body);
            
            //Assert
            Assert.That(samples, Is.Not.Null);
            Assert.That(samples.Length, Is.EqualTo(0));
        }

        [Test]
        public void GetSamples_WithIncompleteData_HandlesGracefully()
        {
            //Arrange
            int bitDepth = 16;
            var body = new byte[] { 0x00, 0x01, 0x02 }; // 3 bytes, incomplete for 2 samples
            
            //Act
            var samples = NetSdrMessageHelper.GetSamples(bitDepth, body);
            
            //Assert
            Assert.That(samples.Length, Is.EqualTo(1)); // Only 1 complete sample
        }

        [Test]
        public void MessageRoundTrip_ControlItem_PreservesData()
        {
            //Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.SetControlItem;
            var originalCode = NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate;
            var originalBody = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A };
            
            //Act
            var message = NetSdrMessageHelper.GetControlItemMessage(originalType, originalCode, originalBody);
            NetSdrMessageHelper.TranslateMessage(
                message, 
                out var actualType, 
                out var actualCode, 
                out _, 
                out var actualBody);
            
            //Assert
            Assert.That(actualType, Is.EqualTo(originalType));
            Assert.That(actualCode, Is.EqualTo(originalCode));
            Assert.That(actualBody, Is.EqualTo(originalBody));
        }

        [Test]
        public void MessageRoundTrip_DataItem_PreservesData()
        {
            //Arrange
            var originalType = NetSdrMessageHelper.MsgTypes.DataItem2;
            var originalBody = new byte[] { 0xFF, 0xEE, 0xDD, 0xCC, 0xBB, 0xAA };
            
            //Act
            var message = NetSdrMessageHelper.GetDataItemMessage(originalType, originalBody);
            NetSdrMessageHelper.TranslateMessage(
                message, 
                out var actualType, 
                out _, 
                out _, 
                out var actualBody);
            
            //Assert
            Assert.That(actualType, Is.EqualTo(originalType));
            Assert.That(actualBody, Is.EqualTo(originalBody));
        }
    }
}
