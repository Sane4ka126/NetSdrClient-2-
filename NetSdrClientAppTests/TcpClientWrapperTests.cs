using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using NetSdrClientApp.Networking;

namespace NetSdrClientAppTests.Networking
{
    public class TcpClientWrapperTests
    {
        [Fact]
        public async Task SendMessageAsync_WithByteArray_ThrowsWhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 5000);
            var data = new byte[] { 0x01, 0x02, 0x03 };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync(data)
            );
        }

        [Fact]
        public async Task SendMessageAsync_WithString_ThrowsWhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 5000);
            var message = "test message";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                async () => await wrapper.SendMessageAsync(message)
            );
        }

        [Fact]
        public void Connected_ReturnsFalse_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 5000);

            // Act
            var isConnected = wrapper.Connected;

            // Assert
            Assert.False(isConnected);
        }

        [Fact]
        public void Disconnect_DoesNotThrow_WhenNotConnected()
        {
            // Arrange
            var wrapper = new TcpClientWrapper("localhost", 5000);

            // Act & Assert - should not throw
            wrapper.Disconnect();
        }

        [Fact]
        public void Constructor_SetsHostAndPort()
        {
            // Arrange & Act
            var wrapper = new TcpClientWrapper("192.168.1.1", 8080);

            // Assert
            Assert.NotNull(wrapper);
            Assert.False(wrapper.Connected);
        }
    }
}
