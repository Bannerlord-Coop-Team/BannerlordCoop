using Common.MessageBroker;
using Coop.Mod.LogicStates;
using Coop.Mod.LogicStates.Client;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Client
{
    public class ClientLogic : IClientLogic
    {
        public ILogger Logger { get; }

        public IState State { get => _state; set => _state = (IClientState)value; }
        private IClientState _state;

        public ICommunicator Communicator { get; }
        
        public ClientLogic(ILogger logger, ICommunicator communicator)
        {
            Logger = logger;
            Communicator = communicator;

            _state = new InitialClientState(this);
        }
    }
}
