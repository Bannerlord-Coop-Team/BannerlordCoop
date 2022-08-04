using Common.MessageBroker;
using System;

namespace Coop.Mod.States.Client
{
    public abstract class ClientState : IClientState
    {
        protected IClientContext _clientContext;

        public ClientState(IClientContext clientContext)
        {
            _clientContext = clientContext;
        }

        public abstract void Connect();
    }
}
