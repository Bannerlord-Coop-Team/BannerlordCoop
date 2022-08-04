using Common.MessageBroker;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.States
{
    public interface IContext
    {
        ILogger Logger { get; }
        IMessageBroker MessageBroker { get; }
        IState State { get; set; }
        IPacketManager PacketManager { get; }
    }
}
