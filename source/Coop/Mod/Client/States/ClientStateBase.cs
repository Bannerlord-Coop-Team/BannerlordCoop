using Common.Messages;
using GameInterface;

namespace Coop.Mod.LogicStates.Client
{
    public abstract class ClientStateBase : IClientState
    {
        protected readonly IClientLogic Logic;
        protected readonly IMessageBroker MessageBroker;

        public ClientStateBase(IClientLogic logic, IMessageBroker messageBroker)
        {
            Logic = logic;
            MessageBroker = messageBroker;
        }
    }
}
