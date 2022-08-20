using Common.Messaging;
using System;

namespace Coop.Core.Server.States
{
    public abstract class ServerStateBase : IServerState
    {
        protected IServerLogic Logic;
        protected IMessageBroker MessageBroker;
        public ServerStateBase(IServerLogic logic, IMessageBroker messageBroker)
        {
            Logic = logic;
            MessageBroker = messageBroker;
        }

        protected ServerStateBase(IServerLogic logic)
        {
            Logic = logic;
        }

        public abstract void Dispose();
        public abstract void Start();
        public abstract void Stop();
    }
}
