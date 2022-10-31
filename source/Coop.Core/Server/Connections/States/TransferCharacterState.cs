using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class TransferCharacterState : ConnectionStateBase
    {
        public TransferCharacterState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) : base(connectionLogic, messageBroker)
        {
        }

        public override void CreateCharacter()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic, MessageBroker);
        }

        public override void ResolveCharacter()
        {
        }

        public override void TransferCharacter()
        {
        }
    }
}
