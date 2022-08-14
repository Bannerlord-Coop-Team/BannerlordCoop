using Common.Messages;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientState : IClientState
    {
        protected IClientLogic Logic;
        protected IMessageBroker MessageBroker;

        public ClientState(IMessageBroker messageBroker, IClientLogic logic)
        {
            MessageBroker = messageBroker;
            Logic = logic;
        }

        public abstract void Connect();
    }
}
