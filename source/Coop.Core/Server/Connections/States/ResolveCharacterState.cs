using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class ResolveCharacterState : ConnectionStateBase
    {
        public ResolveCharacterState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) 
            : base(connectionLogic, messageBroker)
        {
        }

        public override void ResolveCharacter()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic, MessageBroker);
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}
