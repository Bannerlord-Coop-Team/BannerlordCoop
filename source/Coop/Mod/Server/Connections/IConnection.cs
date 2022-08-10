using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Server.Connections
{
    public interface IConnection : IDisposable
    {
        Guid Id { get; }
        NetPeer Peer { get; }
        IConnectionState State { get; }

    }
}
