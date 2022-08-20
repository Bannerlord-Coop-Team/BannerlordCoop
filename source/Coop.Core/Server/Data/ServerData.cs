using Coop.Core.Data;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Server.Data
{
    internal class ServerData
    {
        public IReadOnlyList<ICoopPlayer> Players => _players;
        private readonly List<ICoopPlayer> _players = new List<ICoopPlayer>();
        void AddClient(NetPeer peer)
        {

        }
        void RemoveClient(NetPeer peer)
        {

        }
        void AddP2PClient(NetPeer peer)
        {

        }
        void RemoveP2PClient(NetPeer peer)
        {

        }
    }
}
