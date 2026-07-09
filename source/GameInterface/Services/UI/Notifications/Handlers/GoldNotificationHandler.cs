using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class GoldNotificationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GoldNotificationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public GoldNotificationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<NotifyGoldChanged>(Handle_NotifyGoldChanged);
        messageBroker.Subscribe<NetworkNotifyGoldChanged>(Handle_NetworkNotifyGoldChanged);

        messageBroker.Subscribe<NotifyDailyGoldChange>(Handle_NotifyDailyGoldChange);
        messageBroker.Subscribe<NetworkNotifyDailyGoldChange>(Handle_NetworkNotifyDailyGoldChange);

        messageBroker.Subscribe<NotifyGoldPlundered>(Handle_NotifyGoldPlundered);
        messageBroker.Subscribe<NetworkNotifyGoldPlundered>(Handle_NetworkNotifyGoldPlundered);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyGoldChanged>(Handle_NotifyGoldChanged);
        messageBroker.Unsubscribe<NetworkNotifyGoldChanged>(Handle_NetworkNotifyGoldChanged);

        messageBroker.Unsubscribe<NotifyDailyGoldChange>(Handle_NotifyDailyGoldChange);
        messageBroker.Unsubscribe<NetworkNotifyDailyGoldChange>(Handle_NetworkNotifyDailyGoldChange);

        messageBroker.Unsubscribe<NotifyGoldPlundered>(Handle_NotifyGoldPlundered);
        messageBroker.Unsubscribe<NetworkNotifyGoldPlundered>(Handle_NetworkNotifyGoldPlundered);
    }

    private void Handle_NotifyGoldChanged(MessagePayload<NotifyGoldChanged> obj)
    {
        var data = obj.What;
        
        GameThread.RunSafe(() =>
        {
            // Hero and party ids can be legitimately null for both giver and recipient
            string giverHeroId = null;
            if (data.GiverHero != null && !objectManager.TryGetIdWithLogging(data.GiverHero, out giverHeroId)) return;

            string giverPartyId = null;
            if (data.GiverParty != null && !objectManager.TryGetIdWithLogging(data.GiverParty, out giverPartyId)) return;

            string recipientHeroId = null;
            if (data.RecipientHero != null && !objectManager.TryGetIdWithLogging(data.RecipientHero, out recipientHeroId)) return;

            string recipientPartyId = null;
            if (data.RecipientParty != null && !objectManager.TryGetIdWithLogging(data.RecipientParty, out recipientPartyId)) return;

            network.SendAll(new NetworkNotifyGoldChanged(
                giverHeroId,
                giverPartyId,
                recipientHeroId,
                recipientPartyId,
                data.GoldAmount,
                data.ShowQuickinformation));
        });
    }

    private void Handle_NetworkNotifyGoldChanged(MessagePayload<NetworkNotifyGoldChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            Hero giverHero = null;
            if (data.GiverHeroId != null && !objectManager.TryGetObjectWithLogging(data.GiverHeroId, out giverHero)) return;

            PartyBase giverParty = null;
            if (data.GiverPartyId != null && !objectManager.TryGetObjectWithLogging(data.GiverPartyId, out giverParty)) return;

            Hero recipientHero = null;
            if (data.RecipientHeroId != null && !objectManager.TryGetObjectWithLogging(data.RecipientHeroId, out recipientHero)) return;

            PartyBase recipientParty = null;
            if (data.RecipientPartyId != null && !objectManager.TryGetObjectWithLogging(data.RecipientPartyId, out recipientParty)) return;

            using (new AllowedThread())
            {
                CampaignEventDispatcher.Instance.OnHeroOrPartyTradedGold(
                    new ValueTuple<Hero, PartyBase>(giverHero ?? null, giverParty ?? null),
                    new ValueTuple<Hero, PartyBase>(recipientHero ?? null, recipientParty ?? null),
                    new ValueTuple<int, string>(data.GoldAmount, ""), obj.What.ShowQuickInformation);
            }
        });
    }

    private void Handle_NotifyDailyGoldChange(MessagePayload<NotifyDailyGoldChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

            network.SendAll(new NetworkNotifyDailyGoldChange(clanId, data.GoldChange));
        });
    }

    private void Handle_NetworkNotifyDailyGoldChange(MessagePayload<NetworkNotifyDailyGoldChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.ClanId, out var clan)) return;

            // Only notify client of gold change for their clan
            if (clan != Clan.PlayerClan) return;

            var goldChange = data.GoldChange;
            TextObject textObject = new TextObject("{=dPD5zood}Daily Gold Change: {CHANGE}{GOLD_ICON}", null);
            textObject.SetTextVariable("CHANGE", goldChange);
            textObject.SetTextVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
            string soundEventPath = (goldChange > 0) ? "event:/ui/notification/coins_positive" : ((goldChange == 0) ? string.Empty : "event:/ui/notification/coins_negative");
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), soundEventPath));
        });
    }

    private void Handle_NotifyGoldPlundered(MessagePayload<NotifyGoldPlundered> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.LeaderHero, out var leaderHeroId)) return;

            network.SendAll(new NetworkNotifyGoldPlundered(leaderHeroId, data.PlunderedGold));
        });
    }

    private void Handle_NetworkNotifyGoldPlundered(MessagePayload<NetworkNotifyGoldPlundered> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.LeaderHeroId, out var leaderHero)) return;

            // Only notify client of plundered gold for their hero
            if (leaderHero != Hero.MainHero) return;

            MBTextManager.SetTextVariable("GOLD", data.PlunderedGold);
            MBInformationManager.AddQuickInformation(GameTexts.FindText("str_plunder_gain_message", null), 0, null, null, "");
        });
    }
}