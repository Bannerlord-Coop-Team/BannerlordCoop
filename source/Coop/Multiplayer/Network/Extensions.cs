using Coop.Network;
using LiteNetLib;

namespace Coop.Multiplayer.Network
{
    public static class Extensions
    {
        public static ConnectionBase GetConnection(this NetPeer peer)
        {
            return (ConnectionBase)peer.Tag;
        }
    }
}
