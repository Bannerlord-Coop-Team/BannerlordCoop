using Common.MessageBroker;
using Coop.Mod.EventHandlers;
using Coop.Mod.LogicStates;
using Coop.Mod.LogicStates.Server;
using System;
using System.Collections.Generic;
using Coop.Debug.Logger;

namespace Coop.Mod
{
    public class ServerLogic : IServerLogic
    {
        public IState State { get => _state; set => _state = (IServerState)value; }
        private IServerState _state;

        public ICommunicator Communicator { get; }

        public ServerLogic()
        {
            /* Communicator = communicator;*/

            _state = new InitialServerState(this);
            //RegisterHandler<ExampleHandler>();
        }

        private readonly List<EventHandlerBase> _eventHandlers = new List<EventHandlerBase>();

        private void RegisterHandler<T>() where T : EventHandlerBase
        {
            _eventHandlers.Add((EventHandlerBase)Activator.CreateInstance(typeof(T), new object[] { Communicator }));
        }
    }
}
