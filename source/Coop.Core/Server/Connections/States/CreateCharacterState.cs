using Common.Messaging;
using Coop.Core.Server.Connections.Messages.Incoming;
using Coop.Core.Server.Connections.Messages.Outgoing;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Interfaces;
using LiteNetLib;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class CreateCharacterState : ConnectionStateBase
    {
        private Guid HeroRegistrationTransactionId;

        public CreateCharacterState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Publish(this, new PlayerCreatingCharacter(ConnectionLogic.PlayerId));

            ConnectionLogic.NetworkMessageBroker.Subscribe<NetworkTransferedHero>(PlayerTransferedHeroHandler);
            ConnectionLogic.NetworkMessageBroker.Subscribe<NewPlayerHeroRegistered>(PlayerHeroRegisteredHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<NetworkTransferedHero>(PlayerTransferedHeroHandler);
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<NewPlayerHeroRegistered>(PlayerHeroRegisteredHandler);
        }

        private void PlayerTransferedHeroHandler(MessagePayload<NetworkTransferedHero> obj)
        {
            var playerId = obj.Who as NetPeer;
            
            if(playerId == ConnectionLogic.PlayerId)
            {
                HeroRegistrationTransactionId = Guid.NewGuid();
                var registerCommand = new RegisterNewPlayerHero(HeroRegistrationTransactionId, obj.What.PlayerHero);
                ConnectionLogic.NetworkMessageBroker.Publish(this, registerCommand);
            }
        }
        private void PlayerHeroRegisteredHandler(MessagePayload<NewPlayerHeroRegistered> obj)
        {
            var eventId = obj.What.RegistrationEventId;

            if (HeroRegistrationTransactionId == eventId)
            {
                ConnectionLogic.HeroId = obj.What.HeroGUID;
                ConnectionLogic.TransferSave();
            }
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
        }

        public override void ResolveCharacter()
        {
        }

        public override void TransferSave()
        {
            ConnectionLogic.State = new TransferSaveState(ConnectionLogic);
        }
    }
}
