using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    public readonly struct PlayerTransitionedToCampaign : ICommand
    {
        public NetPeer PlayerId { get; }

        public PlayerTransitionedToCampaign(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
