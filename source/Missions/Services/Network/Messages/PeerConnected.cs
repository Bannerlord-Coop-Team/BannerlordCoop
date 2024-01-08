using Common.Messaging;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Missions.Services.Network.Messages
{
    public readonly struct PeerConnected: IMessage
    {
        public NetPeer Peer { get; }

        public PeerConnected(NetPeer peer)
        {
            Peer = peer;
        }
    }
}
