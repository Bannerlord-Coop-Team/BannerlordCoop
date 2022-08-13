using Coop.Communication.MessageBroker;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientState : IClientState
    {
        protected IClientLogic Logic;
        protected IMessageBroker MessageBroker;

        public ClientState(IClientLogic logic, IMessageBroker messageBroker)
        {
            Logic = logic;
            MessageBroker = messageBroker;
        }

        public abstract void Connect();
    }
}
