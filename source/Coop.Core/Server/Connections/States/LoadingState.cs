using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class LoadingState : ConnectionStateBase
    {
        public LoadingState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) : base(connectionLogic, messageBroker)
        {
        }

        public override void ResolveCharacter()
        {
        }

        public override void Load()
        {
        }

        public override void EnterCampaign()
        {
            ConnectionLogic.State = new CampaignState(ConnectionLogic, MessageBroker);
        }

        public override void EnterMission()
        {
        }
    }
}
