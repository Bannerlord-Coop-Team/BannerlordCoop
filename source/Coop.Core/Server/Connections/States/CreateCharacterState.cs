using Common.Logging;
using Common.Messaging;
using Coop.Core.Server.Connections.Messages;
using GameInterface.Services.Heroes.Handlers;
using GameInterface.Services.Heroes.Interfaces;
using LiteNetLib;
using Serilog;
using Serilog.Core;
using System;

namespace Coop.Core.Server.Connections.States
{
    public class CreateCharacterState : ConnectionStateBase
    {
        private readonly ILogger Logger = LogManager.GetLogger<CreateCharacterState>();

        public CreateCharacterState(IConnectionLogic connectionLogic)
            : base(connectionLogic)
        {
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
            var peerId = ((NetPeer)obj.Who).Id;
            
            if(peerId == ConnectionLogic.PlayerId.Id)
            {
                var registerCommand = new RegisterNewPlayerHero(peerId, obj.What.PlayerHero);
                ConnectionLogic.NetworkMessageBroker.Publish(this, registerCommand);
            }
        }
        private void PlayerHeroRegisteredHandler(MessagePayload<NewPlayerHeroRegistered> obj)
        {
            var peerId = obj.What.PeerId;

            if (peerId == ConnectionLogic.PlayerId.Id)
            {
                NetworkPlayerData playerData = new NetworkPlayerData(obj.What);
                ConnectionLogic.NetworkMessageBroker.PublishNetworkEvent(playerData);

                ConnectionLogic.HeroStringId = obj.What.HeroStringId;
                Logger.Information("Hero StringId: {stringId}", obj.What.HeroStringId);
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

        public override void TransferSave()
        {
            ConnectionLogic.State = new TransferSaveState(ConnectionLogic);
        }
    }
}
