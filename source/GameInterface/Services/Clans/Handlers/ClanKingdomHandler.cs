using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans.Handlers
{
    /// <summary>
    /// Handles all changes to clans on client.
    /// </summary>
    public class ClanKingdomHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClanKingdomHandler>();

        public ClanKingdomHandler(IMessageBroker messageBroker, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ChangeClanKingdom>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ChangeClanKingdom>(Handle);
        }

        private void Handle(MessagePayload<ChangeClanKingdom> obj)
        {
            var payload = obj.What;

            if (objectManager.TryGetObject<Clan>(payload.ClanId, out var clan) == false)
            {
                Logger.Error("Unable to find clan ({clanId})", payload.ClanId);
                return;
            }

            if (objectManager.TryGetObject<Kingdom>(payload.NewKingdomId, out var newKingdom) == false)
            {
                Logger.Error("Unable to find kingdom ({kingdomId})", payload.NewKingdomId);
                return;
            }

            //ClanChangeKingdomPatch.RunOriginalChangeClanKingdom(clan, newKingdom, (ChangeKingdomAction.ChangeKingdomActionDetail)payload.DetailId, 
             //   payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

        }
    }
}