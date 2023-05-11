using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States
{
    public class LoadingState : ConnectionStateBase
    {
        public LoadingState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(PlayerCampaignEnteredHandler);
        }

        private void PlayerCampaignEnteredHandler(MessagePayload<NetworkPlayerCampaignEntered> obj)
        {
            var playerId = (NetPeer)obj.Who;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.EnterCampaign();
                ConnectionLogic.NetworkMessageBroker.Publish(this, new PlayerCampaignEntered());
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
