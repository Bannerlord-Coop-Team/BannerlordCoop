using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Server.Messages.Events
{
    public readonly struct ClientJoinRequested
    {
        NetPeer peer
    }
}
