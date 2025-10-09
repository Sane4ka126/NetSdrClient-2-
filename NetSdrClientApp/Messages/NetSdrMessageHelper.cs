using System;
using System.Collections.Generic;
using System.Linq;

namespace NetSdrClientApp.Messages
{
    public static class NetSdrMessageHelper
    {
        private const short _maxMessageLength = 8191;
        private const short _maxDataItemMessageLength = 8194;
        private const short _msgHeaderLength = 2;
        private const short _msgControlItemLength = 2;
        private const short _msgSequenceNumberLength = 2;

        public enum MsgTypes
        {
            SetControlItem,
            CurrentControlItem,
            ControlItemRange,
            Ack,
            DataItem0,
            DataItem1,
            DataItem2,
            DataItem3
        }

        public enum ControlItemCodes
        {
            None = 0,
            IQOutputDataSampleRate = 0x00B8,
            RFFilter = 0x0044,
            ADModes = 0x008A,
            ReceiverState = 0x0018,
            ReceiverFrequency = 0x0020
        }

        public static byte[] GetControlItemMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            return GetMessage(type, itemCode, parameters);
        }

        public static byte[] GetDataItemMessage(MsgTypes type, byte[] parameters)
        {
            return GetMessage(type, ControlItemCodes.None, parameters);
        }

        private static byte[] GetMessage(MsgTypes type, ControlItemCodes itemCode, byte[] parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var itemCodeBytes = Array.Empty<byte>();
            if (itemCode != ControlItemCodes.None)
            {
                itemCodeBytes = BitConverter.GetBytes((ushort)itemCode);
            }

            var headerBytes = GetHeader(type, itemCodeBytes.Length + parameters.Length);
            var msg = new List<byte>(headerBytes.Length + itemCodeBytes.Length + parameters.Length);
            msg.AddRange(headerBytes);
            msg.AddRange(itemCodeBytes);
            msg.AddRange(parameters);
            return msg.ToArray();
        }

        public static bool TranslateMessage(byte[] msg, out MsgTypes type, out ControlItemCodes itemCode, out ushort sequenceNumber, out byte[] body)
        {
            if (msg == null)
                throw new ArgumentNullException(nameof(msg));

            if (msg.Length < _msgHeaderLength)
            {
                type = default;
                itemCode = ControlItemCodes.None;
                sequenceNumber = 0;
                body = Array.Empty<byte>();
                return false;
            }

            itemCode = ControlItemCodes.None;
            sequenceNumber = 0;
            bool success = true;

            TranslateHeader(msg.Take(_msgHeaderLength).ToArray(), out type, out int msgLength);
            int offset = _msgHeaderLength;
            int remainingLength = msgLength - _msgHeaderLength;

            if (type < MsgTypes.DataItem0)
            {
                if (msg.Length < offset + _msgControlItemLength)
                {
                    body = Array.Empty<byte>();
                    return false;
                }

                var value = BitConverter.ToUInt16(msg, offset);
                offset += _msgControlItemLength;
                remainingLength -= _msgControlItemLength;

                // Convert ushort to int before calling IsDefined
                if (Enum.IsDefined(typeof(ControlItemCodes), (int)value))
                {
                    itemCode = (ControlItemCodes)value;
                }
                else
                {
                    success = false;
                }
            }
            else
            {
                if (msg.Length < offset + _msgSequenceNumberLength)
                {
                    body = Array.Empty<byte>();
                    return false;
                }

                sequenceNumber = BitConverter.ToUInt16(msg, offset);
                offset += _msgSequenceNumberLength;
                remainingLength -= _msgSequenceNumberLength;
            }

            if (msg.Length < offset + remainingLength)
            {
                body = Array.Empty<byte>();
                return false;
            }

            body = new byte[remainingLength];
            Array.Copy(msg, offset, body, 0, remainingLength);
            
            success &= body.Length == remainingLength;
            return success;
        }

        public static IEnumerable<int> GetSamples(ushort sampleSize, byte[] body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            int sampleSizeBytes = sampleSize / 8;

            if (sampleSizeBytes <= 0 || sampleSizeBytes > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleSize), "Sample size must be between 8 and 32 bits");
            }

            int offset = 0;
            var buffer = new byte[4];

            while (offset + sampleSizeBytes <= body.Length)
            {
                Array.Clear(buffer, 0, 4);
                Array.Copy(body, offset, buffer, 0, sampleSizeBytes);
                yield return BitConverter.ToInt32(buffer, 0);
                offset += sampleSizeBytes;
            }
        }

        private static byte[] GetHeader(MsgTypes type, int msgLength)
        {
            int lengthWithHeader = msgLength + _msgHeaderLength;

            if (type >= MsgTypes.DataItem0 && lengthWithHeader == _maxDataItemMessageLength)
            {
                lengthWithHeader = 0;
            }

            if (msgLength < 0 || lengthWithHeader > _maxMessageLength)
            {
                throw new ArgumentException($"Message length {msgLength} exceeds allowed value", nameof(msgLength));
            }

            return BitConverter.GetBytes((ushort)(lengthWithHeader + ((int)type << 13)));
        }

        private static void TranslateHeader(byte[] header, out MsgTypes type, out int msgLength)
        {
            var num = BitConverter.ToUInt16(header, 0);
            type = (MsgTypes)(num >> 13);
            msgLength = num - ((int)type << 13);

            if (type >= MsgTypes.DataItem0 && msgLength == 0)
            {
                msgLength = _maxDataItemMessageLength;
            }
        }
    }
}
