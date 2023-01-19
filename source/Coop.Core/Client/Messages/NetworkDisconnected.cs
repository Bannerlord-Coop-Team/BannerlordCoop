using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Core.Client.Messages
{
    public readonly struct NetworkDisconnected
    {
        public NetworkDisconnected(DisconnectInfo disconnectInfo)
        {
            DisconnectInfo = disconnectInfo;
        }

        public DisconnectInfo DisconnectInfo { get; }
    }
}
