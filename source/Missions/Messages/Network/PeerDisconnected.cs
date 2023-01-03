using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Messages.Network
{
    public readonly struct PeerDisconnected
    {
        public NetPeer NetPeer { get; }
        public DisconnectInfo DisconnectInfo { get; }

        public PeerDisconnected(NetPeer netPeer, DisconnectInfo disconnectInfo)
        {
            NetPeer = netPeer;
            DisconnectInfo = disconnectInfo;
        }
    }
}
