using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Handlers
{
    public class PlayerSurrenderHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<PlayerSurrenderHandler>();

        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;

        public PlayerSurrenderHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<SurrenderLocalPlayer>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SurrenderLocalPlayer>(Handle);
        }

        private void Handle(MessagePayload<SurrenderLocalPlayer> obj)
        {
            var payload = obj.What;

            if(objectManager.TryGetObject(payload.CaptorPartyId, out MobileParty captorParty) == false)
            {
                Logger.Error("Could not find {objType} with string id {stringId}", typeof(MobileParty), payload.CaptorPartyId);
                return;
            }

            SurrenderPatch.RunStartPlayerCaptivity(captorParty.Party);
        }
    }
}