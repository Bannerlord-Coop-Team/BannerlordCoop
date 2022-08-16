using Common.Messages;

namespace Coop.Mod.LogicStates.Client
{
    internal class InitialClientState : ClientStateBase
    {
        public InitialClientState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker)
        {
        }
    }
}
