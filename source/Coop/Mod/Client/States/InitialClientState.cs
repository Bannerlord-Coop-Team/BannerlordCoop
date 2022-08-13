using Coop.Communication.MessageBroker;
using System;

namespace Coop.Mod.LogicStates.Client
{
    internal class InitialClientState : ClientState
    {
        public InitialClientState(IClientLogic logic, IMessageBroker messageBroker) : base(logic, messageBroker) { }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
