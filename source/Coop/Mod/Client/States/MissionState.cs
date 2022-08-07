using Coop.Communication.MessageBroker;
using System;

namespace Coop.Mod.LogicStates.Client
{
    public class MissionState : ClientState
    {
        public MissionState(IMessageBroker messageBroker, IClientLogic logic) : base(messageBroker, logic) { }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
