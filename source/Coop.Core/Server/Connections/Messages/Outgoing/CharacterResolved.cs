using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages.Incoming
{
    public readonly struct CharacterResolved : IEvent
    {
        public NetPeer PlayerId { get; }

        public CharacterResolved(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}
