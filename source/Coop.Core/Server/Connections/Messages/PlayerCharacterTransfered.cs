using LiteNetLib;

namespace Coop.Core.Server.Connections
{
    public readonly struct PlayerCharacterTransfered
    {
        public NetPeer PlayerId { get; }

        public PlayerCharacterTransfered(NetPeer playerId)
        {
            PlayerId = playerId;
        }
    }
}