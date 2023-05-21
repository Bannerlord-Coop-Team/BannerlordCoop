using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Connections.States
{
    public class ResolveCharacterState : ConnectionStateBase
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        public ResolveCharacterState(IConnectionLogic connectionLogic) 
            : base(connectionLogic)
        {
            messageBroker = connectionLogic.MessageBroker;
            network = connectionLogic.Network;

            messageBroker.Subscribe<NetworkClientValidate>(ClientValidateHandler);
            messageBroker.Subscribe<HeroResolved>(ResolveHeroHandler);
            messageBroker.Subscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        }

        public override void Dispose()
        {
            ConnectionLogic.MessageBroker.Unsubscribe<NetworkClientValidate>(ClientValidateHandler);
            ConnectionLogic.MessageBroker.Unsubscribe<HeroResolved>(ResolveHeroHandler);
            ConnectionLogic.MessageBroker.Unsubscribe<ResolveHeroNotFound>(HeroNotFoundHandler);
        }

        internal void ClientValidateHandler(MessagePayload<NetworkClientValidate> obj)
        {
            var playerId = obj.Who as NetPeer;
            if (playerId != ConnectionLogic.Peer) return;

            ConnectionLogic.MessageBroker.Publish(this, new ResolveDebugHero(obj.What.PlayerId));
        }

        internal void ResolveHeroHandler(MessagePayload<HeroResolved> obj)
        {
            var validateMessage = new NetworkClientValidated(true, obj.What.HeroId);
            var playerPeer = ConnectionLogic.Peer;
            ConnectionLogic.Network.Send(playerPeer, validateMessage);
            ConnectionLogic.TransferSave();
        }

        internal void HeroNotFoundHandler(MessagePayload<ResolveHeroNotFound> obj)
        {
            var validateMessage = new NetworkClientValidated(false, string.Empty);
            var playerPeer = ConnectionLogic.Peer;
            ConnectionLogic.Network.Send(playerPeer, validateMessage);
            ConnectionLogic.CreateCharacter();
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
