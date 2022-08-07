using Coop.Communication.MessageBroker;
using System;

namespace Coop.Mod.LogicStates.Client
{
    internal class MapState : ClientState
    {
        public MapState(IMessageBroker messageBroker, IClientLogic logic) : base(messageBroker, logic) { }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
