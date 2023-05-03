using Common.Network.Data;
using LiteNetLib;

namespace Common.Tests.Data
{
    public class ConnectionTokenTests
    {
        [Fact]
        public void Constructor_WithValidParameters_ShouldCreateInstance()
        {
            // Arrange
            Guid peerId = Guid.NewGuid();
            string instanceName = "TestInstance";
            NatAddressType natType = NatAddressType.Internal;

            // Act
            ConnectionToken connectionToken = new ConnectionToken(peerId, instanceName, natType);

            // Assert
            Assert.NotNull(connectionToken);
            Assert.Equal(peerId, connectionToken.PeerId);
            Assert.Equal(instanceName, connectionToken.InstanceName);
            Assert.Equal(natType, connectionToken.NatType);
        }

        [Fact]
        public void Constructor_WithEmptyPeerId_ShouldThrowArgumentException()
        {
            // Arrange
            Guid peerId = Guid.Empty;
            string instanceName = "TestInstance";
            NatAddressType natType = NatAddressType.External;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ConnectionToken(peerId, instanceName, natType));
        }

        [Fact]
        public void Constructor_WithNullOrEmptyInstanceName_ShouldThrowArgumentException()
        {
            // Arrange
            Guid peerId = Guid.NewGuid();
            string instanceName = null;
            NatAddressType natType = NatAddressType.Internal;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => new ConnectionToken(peerId, instanceName, natType));

            instanceName = string.Empty;

            Assert.Throws<ArgumentException>(() => new ConnectionToken(peerId, instanceName, natType));
        }

        [Fact]
        public void Stringify_Full()
        {
            // Arrange
            Guid peerId = Guid.NewGuid();
            string instanceName = "TestString";
            NatAddressType natType = NatAddressType.Internal;

            // Act & Assert

            ConnectionToken connectionToken = new ConnectionToken(peerId, instanceName, natType);

            string connectionTokenString = connectionToken;

            ConnectionToken resolvedToken = connectionTokenString;

            Assert.Equal(resolvedToken, connectionToken);
        }
    }
}
