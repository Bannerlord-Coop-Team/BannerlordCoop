using Common.Messaging;

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

        ~ServerStateBase()
        {
            Dispose();
        }

        public abstract void Dispose();
        public abstract void Start();
        public abstract void Stop();
    }
}
