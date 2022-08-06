using Common.Components;
using Common.MessageBroker;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates
{
    public interface ILogic : IComponent
    {
        ICommunicator Communicator { get; }
        IState State { get; set; }
    }
}
