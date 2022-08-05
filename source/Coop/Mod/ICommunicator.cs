using Common.MessageBroker;
using Coop.Mod.GameInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod
{
    public interface ICommunicator
    {
        IMessageBroker MessageBroker { get; }
        IPacketManager PacketManager { get; }
        IGameInterface GameInterface { get; }
    }
}
