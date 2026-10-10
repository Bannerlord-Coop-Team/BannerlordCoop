using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Client.Messages
{
    /// <summary>
    /// Network disconnected event
    /// </summary>
    public record NetworkDisconnected : IEvent
    {
        public DisconnectInfo DisconnectInfo { get; }
        public string ServerReason { get; }

        public NetworkDisconnected(DisconnectInfo disconnectInfo, string serverReason = null)
        {
            DisconnectInfo = disconnectInfo;
            ServerReason = serverReason;
        }
    }
}
