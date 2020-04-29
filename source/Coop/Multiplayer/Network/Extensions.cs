using System.Net;
using Coop.Network;
using LiteNetLib;

namespace Coop.Multiplayer.Network
{
    public static class Extensions
    {
        public static ConnectionBase GetConnection(this NetPeer peer)
        {
            return (ConnectionBase) peer.Tag;
        }

        public static string ToFriendlyString(this IPEndPoint endPoint)
        {
            return $"{endPoint.Address}:{endPoint.Port}";
        }

        public static string ToFriendlyString(this ConnectionRequest request)
        {
            return $"{request.RemoteEndPoint.ToFriendlyString()}";
        }
    }
}
