using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Server.Connections
{
    public class Connection : IConnection
    {
        public Guid Id => Guid.NewGuid();

        public NetPeer Peer { get; }

        public IConnectionState State { get; }

        public Connection(NetPeer peer)
        {
            Peer = peer;
            State = new InitialConnectionState(this);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
