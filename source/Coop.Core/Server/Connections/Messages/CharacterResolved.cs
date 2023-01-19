using Common.Messaging;
using LiteNetLib;

namespace Coop.Core.Server.Connections.Messages
{
    public readonly struct CharacterResolved : ICommand
    {
        public CharacterResolved(NetPeer playerId)
        {
            PlayerId = playerId;
        }

        public NetPeer PlayerId { get; }
    }
}
