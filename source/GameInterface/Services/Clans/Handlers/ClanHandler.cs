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
    public class ClanHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly IObjectManager objectManager;
        private readonly ILogger Logger = LogManager.GetLogger<ClanHandler>();

        public ClanHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            this.objectManager = objectManager;
            messageBroker.Subscribe<ClanNameChanged>(Handle);
            messageBroker.Subscribe<ClanLeaderChanged>(Handle);
            messageBroker.Subscribe<ClanKingdomChanged>(Handle);
            messageBroker.Subscribe<ClanDestroyed>(Handle);
            messageBroker.Subscribe<CompanionAdded>(Handle);
            messageBroker.Subscribe<RenownAdded>(Handle);
            messageBroker.Subscribe<ClanInfluenceChanged>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ClanNameChanged>(Handle);
        }

        private void Handle(MessagePayload<ClanNameChanged> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            ClanNameChangePatch.RunOriginalChangeClanName(clan, new TextObject(payload.Name), new TextObject(payload.InformalName));
        }

        private void Handle(MessagePayload<ClanLeaderChanged> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            if(payload.NewLeaderId != null)
            {
                Hero newLeader = Hero.FindFirst(x => x.StringId == payload.NewLeaderId);
                ClanLeaderChangePatch.RunOriginalChangeClanLeader(clan, newLeader);
                return;
            }

            ClanLeaderChangePatch.RunOriginalChangeClanLeader(clan);

        }

        private void Handle(MessagePayload<ClanKingdomChanged> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            Kingdom newKingdom = Kingdom.All.Find(x => x.StringId == payload.NewKingdomId);

            ClanChangeKingdomPatch.RunOriginalChangeClanKingdom(clan, newKingdom, (ChangeKingdomAction.ChangeKingdomActionDetail)payload.DetailId, 
                payload.AwardMultiplier, payload.ByRebellion, payload.ShowNotification);

        }

        private void Handle(MessagePayload<ClanDestroyed> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            ClanDestroyPatch.RunOriginalDestroyClan(clan, payload.Details);
        }

        private void Handle(MessagePayload<CompanionAdded> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            Hero companion = Hero.FindFirst(x => x.StringId == payload.CompanionId);

            ClanAddCompanionPatch.RunOriginalAddCompanion(clan, companion);
        }
        private void Handle(MessagePayload<RenownAdded> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            ClanAddRenownPatch.RunOriginalAddCompanion(clan, payload.Amount, payload.ShouldNotify);
        }
        private void Handle(MessagePayload<ClanInfluenceChanged> obj)
        {
            var payload = obj.What;

            Clan clan = Clan.FindFirst(x => x.StringId == payload.ClanId);

            ClanChangeInfluencePatch.RunOriginalChangeClanInfluence(clan, payload.Amount);
        }
    }
}
