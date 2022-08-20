using Coop.Core.Server.Connections.States;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections
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
