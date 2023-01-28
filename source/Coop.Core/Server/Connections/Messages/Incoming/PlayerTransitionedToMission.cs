using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    public readonly struct PlayerTransitionedToMission : ICommand
    {
        public NetPeer PlayerId { get; }

        public PlayerTransitionedToMission(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
