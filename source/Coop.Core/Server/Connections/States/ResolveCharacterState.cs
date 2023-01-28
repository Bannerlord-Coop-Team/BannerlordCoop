using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;
using Coop.Core.Server.Connections.Messages.Outgoing;

namespace Coop.Core.Server.Connections.States
{
    public class ResolveCharacterState : ConnectionStateBase
    {
        public ResolveCharacterState(IConnectionLogic connectionLogic) 
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<CharacterResolved>(PlayerJoinedHandler);
            ConnectionLogic.NetworkMessageBroker.Subscribe<PlayerCreatingCharacter>(PlayerCreatingCharacterHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<CharacterResolved>(PlayerJoinedHandler);
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<PlayerCreatingCharacter>(PlayerCreatingCharacterHandler);
        }

        private void PlayerJoinedHandler(MessagePayload<CharacterResolved> obj)
        {
            var playerId = obj.What.PlayerId;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.Load();
            }
        }

        private void PlayerCreatingCharacterHandler(MessagePayload<PlayerCreatingCharacter> obj)
        {
            var playerId = obj.What.PlayerId;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.CreateCharacter();
            }
        }

        public override void ResolveCharacter()
        {
        }

        public override void CreateCharacter()
        {
            ConnectionLogic.State = new CreateCharacterState(ConnectionLogic);
        }

        public override void TransferSave()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic);
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}
