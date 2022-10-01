using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class CampaignState : ConnectionStateBase
    {
        public CampaignState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) : base(connectionLogic, messageBroker)
        {

        }

        public override void Join()
        {
        }

        public override void Load()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
            ConnectionLogic.State = new MissionState(ConnectionLogic, MessageBroker);
        }
    }
}
