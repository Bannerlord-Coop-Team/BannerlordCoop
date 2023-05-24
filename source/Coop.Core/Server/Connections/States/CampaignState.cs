using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class CampaignState : ConnectionStateBase
    {
        public CampaignState(IConnectionLogic connectionLogic) : base(connectionLogic)
        {
            ConnectionLogic.MessageBroker.Subscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.MessageBroker.Unsubscribe<NetworkPlayerMissionEntered>(PlayerMissionEnteredHandler);
        }

        internal void PlayerMissionEnteredHandler(MessagePayload<NetworkPlayerMissionEntered> obj)
        {
            var playerId = (NetPeer)obj.Who;

            if (playerId == ConnectionLogic.Peer)
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
