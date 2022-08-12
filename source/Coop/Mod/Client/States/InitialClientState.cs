using Coop.Communication.MessageBroker;
using System;

namespace Coop.Mod.LogicStates.Client
{
    internal class InitialClientState : ClientState
    {
        public InitialClientState(IMessageBroker messageBroker, IClientLogic logic) : base(messageBroker, logic) { }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
