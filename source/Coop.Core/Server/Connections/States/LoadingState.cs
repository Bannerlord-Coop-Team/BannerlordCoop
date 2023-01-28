using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;
using Coop.Core.Server.Connections.Messages.Outgoing;
using GameInterface.Services.Time.Messages;

namespace Coop.Core.Server.Connections.States
{
    public class LoadingState : ConnectionStateBase
    {
        public LoadingState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PlayerLoading(ConnectionLogic.PlayerId));
            ConnectionLogic.NetworkMessageBroker.Subscribe<PlayerLoaded>(PlayerLoadedHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<PlayerLoaded>(PlayerLoadedHandler);
        }

        private void PlayerLoadedHandler(MessagePayload<PlayerLoaded> obj)
        {
            var playerId = obj.What.PlayerId;
            
            if(playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.EnterCampaign();
            }
        }

        

        public override void ResolveCharacter()
        {
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
            ConnectionLogic.NetworkMessageBroker.Publish(this, new NetworkEnableTimeControls());
            ConnectionLogic.NetworkMessageBroker.Publish(this, new EnableGameTimeControls());

            ConnectionLogic.State = new CampaignState(ConnectionLogic);
        }

        public override void EnterMission()
        {
        }
    }
}
