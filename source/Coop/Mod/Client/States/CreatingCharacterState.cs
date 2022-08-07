using Coop.Communication.MessageBroker;

namespace Coop.Mod.LogicStates.Client
{
    public class CreatingCharacterState : ClientState
    {
        public CreatingCharacterState(IMessageBroker messageBroker, IClientLogic logic) : base(messageBroker, logic) { }


        public override void Connect()
        {
            
        }
    }
}
