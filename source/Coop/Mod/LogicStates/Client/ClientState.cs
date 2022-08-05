using Common.MessageBroker;
using System;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientState : IClientState
    {
        protected IClientLogic _clientContext;

        public ClientState(IClientLogic clientContext)
        {
            _clientContext = clientContext;
        }

        public abstract void Connect();
    }
}
