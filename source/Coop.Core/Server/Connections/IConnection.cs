using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections
{
    public interface IConnection : IDisposable
    {
        Guid Id { get; }
        NetPeer Peer { get; }
        IConnectionState State { get; }
    }
}
