using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States
{
    public class CampaignState : ConnectionStateBase
    {
        public CampaignState(IConnectionLogic connectionLogic) : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
        }

        private void PlayerMissionEnteredHandler(MessagePayload<NetworkPlayerMissionEntered> obj)
        {
            var playerId = (NetPeer)obj.Who;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.EnterMission();
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
        }

        public override void EnterMission()
        {
            ConnectionLogic.State = new MissionState(ConnectionLogic);
        }
    }
}
