using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;

namespace Coop.Core.Server.Connections.States
{
    public class CampaignState : ConnectionStateBase
    {
        public CampaignState(IConnectionLogic connectionLogic) : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<PlayerTransitionedToMission>(PlayerTransitionsMissionHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<PlayerTransitionedToMission>(PlayerTransitionsMissionHandler);
        }

        private void PlayerTransitionsMissionHandler(MessagePayload<PlayerTransitionedToMission> obj)
        {
            ConnectionLogic.EnterMission();
        }

        public override void CreateCharacter()
        {
        }

        public override void TransferSave()
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
            ConnectionLogic.State = new MissionState(ConnectionLogic);
        }
    }
}
