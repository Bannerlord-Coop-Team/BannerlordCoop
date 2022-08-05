using Common.MessageBroker;
using Coop.Mod.EventHandlers;
using Coop.Mod.LogicStates;
using Coop.Mod.LogicStates.Server;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod
{
    public class ServerLogic : IServerLogic
    {
        public ILogger Logger { get; }

        public IState State { get => _state; set => _state = (IServerState)value; }
        private IServerState _state;

        public ICommunicator Communicator { get; }

        public ServerLogic(ILogger logger, ICommunicator communicator)
        {
            Logger = logger;
            Communicator = communicator;

            _state = new InitialServerState(this);

            RegisterHandler<ExampleHandler>();
        }

        private readonly List<EventHandlerBase> _eventHandlers = new List<EventHandlerBase>();

        private void RegisterHandler<T>() where T : EventHandlerBase
        {
            _eventHandlers.Add((EventHandlerBase)Activator.CreateInstance(typeof(T), new object[] { Communicator }));
        }
    }
}
