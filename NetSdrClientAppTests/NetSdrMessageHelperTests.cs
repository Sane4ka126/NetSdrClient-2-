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
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(actualCode, Is.EqualTo((short)code));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            Assert.That(headerBytes.Count(), Is.EqualTo(2));
            Assert.That(msg.Length, Is.EqualTo(actualLength));
            Assert.That(type, Is.EqualTo(actualType));
            Assert.That(parametersBytes.Count(), Is.EqualTo(parametersLength));
        }

        // Нові тести
        [Test]
        public void GetControlItemMessage_EmptyParameters_ReturnsValidMessage()
        {
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            byte[] emptyParams = Array.Empty<byte>();

            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, emptyParams);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            Assert.That(msg.Length, Is.EqualTo(4)); // 2 (header) + 2 (code)
            Assert.That(actualType, Is.EqualTo(type));
            Assert.That(BitConverter.ToInt16(codeBytes.ToArray()), Is.EqualTo((short)code));
            Assert.That(parametersBytes.Count(), Is.EqualTo(0));
        }

        [Test]
        public void GetDataItemMessage_InvalidType_ThrowsException()
        {
            var invalidType = (NetSdrMessageHelper.MsgTypes)999; // Некоректний тип
            byte[] parameters = new byte[100];

            Assert.Throws<ArgumentException>(() => NetSdrMessageHelper.GetDataItemMessage(invalidType, parameters));
        }
    }
}
