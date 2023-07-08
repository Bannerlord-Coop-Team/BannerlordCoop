using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using static TaleWorlds.CampaignSystem.Actions.ChangeKingdomAction;

namespace GameInterface.Services.Kingdoms.Handler
{
    /// <summary>
    /// Handles all changes to kingdoms.
    /// </summary>
    public class KingdomUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ILogger Logger = LogManager.GetLogger<KingdomUpdateHandler>();

        public KingdomUpdateHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            messageBroker.Subscribe<UpdatedKingdomRelation>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<UpdatedKingdomRelation>(Handle);
        }

        private void Handle(MessagePayload<UpdatedKingdomRelation> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);
            Kingdom kingdom = Kingdom.All.Find(x => x.StringId == payload.KingdomId);

            ChangeKingdomActionPatch.RunOriginalApplyInternal(clan, kingdom, (ChangeKingdomActionDetail)payload.ChangeKingdomActionDetail,
                payload.awardMultiplier, payload.byRebellion, payload.showNotification);
        }
    }
}
