using Coop.Communication.MessageBroker;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientState : IClientState
    {
        protected IClientLogic _logic;
        protected IMessageBroker _messageBroker;

        public ClientState(IMessageBroker messageBroker, IClientLogic logic)
        {
            _messageBroker = messageBroker;
            _logic = logic;
        }

        public abstract void Connect();
    }
}
