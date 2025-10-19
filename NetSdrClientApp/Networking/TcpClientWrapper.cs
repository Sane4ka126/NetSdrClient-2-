using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public class TcpClientWrapper : ITcpClient
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            _tcpClient = new TcpClient();

            try
            {
                _cts = new CancellationTokenSource();
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                _ = StartListeningAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (Connected)
            {
                CleanupResources();
                Console.WriteLine("Disconnected.");
            }
            else
            {
                Console.WriteLine("No active connection to disconnect.");
            }
        }

        // ✅ Тепер просто конвертує string -> byte[] і викликає основний метод
        public Task SendMessageAsync(string str)
        {
            var data = Encoding.UTF8.GetBytes(str);
            return SendMessageAsync(data);
        }

        // ✅ Єдине місце з логікою відправки
        public async Task SendMessageAsync(byte[] data)
        {
            ValidateConnection();
            LogMessageSent(data);
            await SendDataAsync(data);
        }

        private void ValidateConnection()
        {
            if (!IsStreamWritable())
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        private void LogMessageSent(byte[] data)
        {
            var hexString = string.Join(" ", data.Select(b => b.ToString("x2")));
            Console.WriteLine($"Message sent: {hexString}");
        }

        private async Task SendDataAsync(byte[] data)
        {
            await _stream!.WriteAsync(new ReadOnlyMemory<byte>(data), _cts?.Token ?? CancellationToken.None);
        }

        private bool IsStreamWritable()
        {
            return Connected && _stream != null && _stream.CanWrite;
        }

        private void CleanupResources()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _stream?.Close();
            _tcpClient?.Close();

            _cts = null;
            _tcpClient = null;
            _stream = null;
        }

        private async Task StartListeningAsync()
        {
            if (!Connected || _stream == null || !_stream.CanRead)
            {
                throw new InvalidOperationException("Not connected to a server.");
            }

            try
            {
                Console.WriteLine($"Starting listening for incoming messages.");

                while (!(_cts?.Token.IsCancellationRequested ?? true))
                {
                    byte[] buffer = new byte[8194];

                    int bytesRead = await _stream.ReadAsync(new Memory<byte>(buffer), _cts?.Token ?? CancellationToken.None);
                    if (bytesRead > 0)
                    {
                        MessageReceived?.Invoke(this, buffer.AsSpan(0, bytesRead).ToArray());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation, no action needed
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listening loop: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
            }
        }
    }
}
