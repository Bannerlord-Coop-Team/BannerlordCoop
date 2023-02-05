using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States
{
    public class MissionState : ConnectionStateBase
    {
        public MissionState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<PlayerTransitionedToCampaign>(PlayerTransitionsCampaignHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<PlayerTransitionedToCampaign>(PlayerTransitionsCampaignHandler);
        }

        private void PlayerTransitionsCampaignHandler(MessagePayload<PlayerTransitionedToCampaign> obj)
        {
            var playerId = (NetPeer)obj.Who;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.EnterCampaign();
            }
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
            ConnectionLogic.State = new CampaignState(ConnectionLogic);
        }

        public override void EnterMission()
        {
        }
    }
}
