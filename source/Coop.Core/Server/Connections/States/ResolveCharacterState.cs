using Common.Messaging;
using Coop.Core.Client.Messages;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;
using System;
using System.Threading.Tasks;

namespace Coop.Core.Server.Connections.States
{
    public class ResolveCharacterState : ConnectionStateBase
    {
        public ResolveCharacterState(IConnectionLogic connectionLogic) 
            : base(connectionLogic)
        {
            ConnectionLogic.NetworkMessageBroker.Subscribe<NetworkClientValidate>(ClientValidateHandler);
            ConnectionLogic.NetworkMessageBroker.Subscribe<HeroResolved>(ResolveHeroHandler);
            ConnectionLogic.NetworkMessageBroker.Subscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<NetworkClientValidate>(ClientValidateHandler);
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<HeroResolved>(ResolveHeroHandler);
            ConnectionLogic.NetworkMessageBroker.Unsubscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        }

        private void ClientValidateHandler(MessagePayload<NetworkClientValidate> obj)
        {
            var playerId = obj.Who as NetPeer;

            if (playerId == ConnectionLogic.PlayerId)
            {
                ConnectionLogic.NetworkMessageBroker.Publish(this, new ResolveDebugHero(playerId.Id, obj.What.PlayerId));
            }
        }

        private void ResolveHeroHandler(MessagePayload<HeroResolved> obj)
        {
            if (obj.What.PeerId == ConnectionLogic.PlayerId.Id)
            {
                var validateMessage = new NetworkClientValidated(true, obj.What.HeroStringId);
                ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(validateMessage);
                ConnectionLogic.TransferSave();
            }
        }

        private void HeroNotFoundHandler(MessagePayload<ResolveHeroNotFound> obj)
        {
            if (obj.What.PeerId == ConnectionLogic.PlayerId.Id)
            {
                var validateMessage = new NetworkClientValidated(false, string.Empty);
                ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(validateMessage);
                ConnectionLogic.CreateCharacter();
            }
        }

        public override void CreateCharacter()
        {
            ConnectionLogic.State = new CreateCharacterState(ConnectionLogic);
        }

        public override void TransferSave()
        {
            ConnectionLogic.State = new TransferSaveState(ConnectionLogic);
        }

        public override void Load()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}
