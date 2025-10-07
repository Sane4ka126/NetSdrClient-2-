using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp
{
    public class NetSdrClient
    {
        private readonly ITcpClient _tcpClient;
        private readonly IUdpClient _udpClient;
        private TaskCompletionSource<byte[]> responseTaskSource;

        public bool IQStarted { get; set; }
        public event EventHandler<byte[]>? MessageReceived; // Made nullable to fix CS8618

        public NetSdrClient(ITcpClient tcpClient, IUdpClient udpClient)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _udpClient = udpClient ?? throw new ArgumentNullException(nameof(udpClient));
            responseTaskSource = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _tcpClient.MessageReceived += _tcpClient_MessageReceived;
            _udpClient.MessageReceived += _udpClient_MessageReceived;
        }

        public async Task ConnectAsync()
        {
            if (!_tcpClient.Connected)
            {
                _tcpClient.Connect();

                var sampleRate = BitConverter.GetBytes((long)100000).Take(5).ToArray();
                var automaticFilterMode = BitConverter.GetBytes((ushort)0).ToArray();
                var adMode = new byte[] { 0x00, 0x03 };

                var msgs = new List<byte[]>
                {
                    NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.IQOutputDataSampleRate, sampleRate),
                    NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.RFFilter, automaticFilterMode),
                    NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.ADModes, adMode),
                };

                foreach (var msg in msgs)
                {
                    await SendTcpRequest(msg);
                }
            }
        }

        public void Disconnect()
        {
            _tcpClient.Disconnect();
        }

        public async Task SendMessageAsync(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (_tcpClient.Connected)
            {
                await _tcpClient.SendMessageAsync(data);
            }
        }

        public async Task StartIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var iqDataMode = (byte)0x80;
            var start = (byte)0x02;
            var fifo16bitCaptureMode = (byte)0x01;
            var n = (byte)1;

            var args = new[] { iqDataMode, start, fifo16bitCaptureMode, n };

            var msg = NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.ReceiverState, args);

            await SendTcpRequest(msg);

            IQStarted = true;

            await _udpClient.StartListeningAsync();
        }

        public async Task StopIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var stop = (byte)0x01;
            var args = new byte[] { 0, stop, 0, 0 };

            var msg = NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.ReceiverState, args);

            await SendTcpRequest(msg);

            IQStarted = false;

            _udpClient.StopListening();
        }

        public async Task ChangeFrequencyAsync(long hz, int channel)
        {
            var channelArg = (byte)channel;
            var frequencyArg = BitConverter.GetBytes(hz).Take(5);
            var args = new[] { channelArg }.Concat(frequencyArg).ToArray();

            var msg = NetSdrMessageHelper.GetControlItemMessage(NetSdrMessageHelper.MsgTypes.SetControlItem, NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency, args);

            await SendTcpRequest(msg);
        }

        private void _udpClient_MessageReceived(object? sender, byte[] e)
        {
            if (e == null) return;

            NetSdrMessageHelper.TranslateMessage(e, out var _, out var _, out var _, out byte[] body); // Removed unused variables
            var samples = NetSdrMessageHelper.GetSamples(16, body);

            Console.WriteLine($"Samples received: {string.Join(" ", body.Select(b => Convert.ToString(b, 16)))}");

            using (var fs = new FileStream("samples.bin", FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new BinaryWriter(fs))
            {
                foreach (var sample in samples)
                {
                    sw.Write((short)sample);
                }
            }
        }

        private async Task<byte[]> SendTcpRequest(byte[] msg)
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return Array.Empty<byte>();
            }

            responseTaskSource = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            await _tcpClient.SendMessageAsync(msg);
            return await responseTaskSource.Task;
        }

        private void _tcpClient_MessageReceived(object? sender, byte[] e)
        {
            if (e == null) return;

            MessageReceived?.Invoke(this, e);
            responseTaskSource?.TrySetResult(e);
        }
    }
}
