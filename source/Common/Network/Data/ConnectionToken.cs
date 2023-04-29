using Common.Logging;
using LiteNetLib;
using Serilog;
using System;

namespace Common.Network.Data
{
    /// <summary>
    /// Represents a connection token containing information required for connecting to a game instance.
    /// </summary>
    public class ConnectionToken
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ConnectionToken>();

        public Guid PeerId { get; }
        public string InstanceName { get; }
        public NatAddressType NatType { get; }

        /// <summary>
        /// Initializes a new instance of the ConnectionToken class with the specified peer identifier, game instance name, and NAT type.
        /// </summary>
        /// <param name="peerId">The unique identifier of the peer associated with the connection token.</param>
        /// <param name="instanceName">The name of the game instance associated with the connection token.</param>
        /// <param name="natType">The type of the NAT associated with the connection token.</param>
        /// <exception cref="ArgumentException">Thrown when the peer identifier is an empty GUID or the instance name is null or empty.</exception>
        public ConnectionToken(Guid peerId, string instanceName, NatAddressType natType)
        {
            if (peerId == Guid.Empty)
            {
                throw new ArgumentException("PeerId cannot be an empty Guid", nameof(peerId));
            }

            if (string.IsNullOrEmpty(instanceName))
            {
                throw new ArgumentException("InstanceName cannot be null or empty", nameof(instanceName));
            }

            PeerId = peerId;
            InstanceName = instanceName;
            NatType = natType;
        }

        public static bool TryParse(string stringToken, out ConnectionToken connectionToken)
        {
            connectionToken = null;
            try
            {
                connectionToken = stringToken;
                return true;
            }
            catch (ArgumentException e)
            {
                Logger.Error("Unable to parse token: {err}", e);
            }

            return false;
        }

        public static implicit operator ConnectionToken(string tokenString)
        {
            string[] data = tokenString.Split('%');
            if (data.Length != 3)
            {
                throw new ArgumentException("Invalid data length, expected 3 but got " + data.Length, nameof(tokenString));
            }

            if (!Guid.TryParse(data[0], out Guid peerId))
            {
                throw new ArgumentException("Invalid PeerId in token string", nameof(tokenString));
            }

            string instanceName = data[1];
            if (string.IsNullOrEmpty(instanceName))
            {
                throw new ArgumentException("InstanceName cannot be null or empty", nameof(tokenString));
            }

            if (Enum.TryParse(data[2], out NatAddressType natType) == false)
            {
                throw new ArgumentException("Invalid NatType in token string", nameof(tokenString));
            }

            return new ConnectionToken(peerId, instanceName, natType);
        }

        public static implicit operator string(ConnectionToken token)
        {
            return string.Join("%", token.PeerId, token.InstanceName, token.NatType);
        }
    }
}
