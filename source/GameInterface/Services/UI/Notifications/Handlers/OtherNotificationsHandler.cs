using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Notifications.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.UI.Notifications.Handlers;

internal class OtherNotificationsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<OtherNotificationsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public OtherNotificationsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<NotifyAnimalsSlaughteredToEat>(Handle_NotifyAnimalsSlaughteredToEat);
        messageBroker.Subscribe<NetworkNotifyAnimalsSlaughteredToEat>(Handle_NetworkNotifyAnimalsSlaughteredToEat);

        messageBroker.Subscribe<NotifyDailyStarvationPenalty>(Handle_NotifyDailyStarvationPenalty);
        messageBroker.Subscribe<NetworkNotifyDailyStarvationPenalty>(Handle_NetworkNotifyDailyStarvationPenalty);

        messageBroker.Subscribe<NotifyAnimalsBred>(Handle_NotifyAnimalsBred);
        messageBroker.Subscribe<NetworkNotifyAnimalsBred>(Handle_NetworkNotifyAnimalsBred);

        messageBroker.Subscribe<NotifyFoundItemOnMap>(Handle_NotifyFoundItemOnMap);
        messageBroker.Subscribe<NetworkNotifyFoundItemOnMap>(Handle_NetworkNotifyFoundItemOnMap);

        messageBroker.Subscribe<NotifyKingdomInfluenceChanged>(Handle_NotifyKingdomInfluenceChanged);
        messageBroker.Subscribe<NetworkNotifyKingdomInfluenceChanged>(Handle_NetworkNotifyKingdomInfluenceChanged);

        messageBroker.Subscribe<NetworkNotifyRemovedSupporter>(Handle_NetworkNotifyRemovedSupporter);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NotifyAnimalsSlaughteredToEat>(Handle_NotifyAnimalsSlaughteredToEat);
        messageBroker.Unsubscribe<NetworkNotifyAnimalsSlaughteredToEat>(Handle_NetworkNotifyAnimalsSlaughteredToEat);

        messageBroker.Unsubscribe<NotifyDailyStarvationPenalty>(Handle_NotifyDailyStarvationPenalty);
        messageBroker.Unsubscribe<NetworkNotifyDailyStarvationPenalty>(Handle_NetworkNotifyDailyStarvationPenalty);

        messageBroker.Unsubscribe<NotifyAnimalsBred>(Handle_NotifyAnimalsBred);
        messageBroker.Unsubscribe<NetworkNotifyAnimalsBred>(Handle_NetworkNotifyAnimalsBred);

        messageBroker.Unsubscribe<NotifyFoundItemOnMap>(Handle_NotifyFoundItemOnMap);
        messageBroker.Unsubscribe<NetworkNotifyFoundItemOnMap>(Handle_NetworkNotifyFoundItemOnMap);

        messageBroker.Unsubscribe<NotifyKingdomInfluenceChanged>(Handle_NotifyKingdomInfluenceChanged);
        messageBroker.Unsubscribe<NetworkNotifyKingdomInfluenceChanged>(Handle_NetworkNotifyKingdomInfluenceChanged);

        messageBroker.Unsubscribe<NetworkNotifyRemovedSupporter>(Handle_NetworkNotifyRemovedSupporter);
    }

    private void Handle_NotifyAnimalsSlaughteredToEat(MessagePayload<NotifyAnimalsSlaughteredToEat> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyAnimalsSlaughteredToEat(mobilePartyId));
        });
    }

    private void Handle_NetworkNotifyAnimalsSlaughteredToEat(MessagePayload<NetworkNotifyAnimalsSlaughteredToEat> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            MBInformationManager.AddQuickInformation(new TextObject("{=WTwafRTH}Your party has slaughtered some animals to eat.", null), 0, null, null, "");
        });
    }

    private void Handle_NotifyDailyStarvationPenalty(MessagePayload<NotifyDailyStarvationPenalty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyDailyStarvationPenalty(mobilePartyId, obj.What.DailyStarvationMoralePenalty));
        });
    }

    private void Handle_NetworkNotifyDailyStarvationPenalty(MessagePayload<NetworkNotifyDailyStarvationPenalty> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            MBTextManager.SetTextVariable("MORALE_PENALTY", obj.What.DailyStarvationMoralePenalty);
            MBInformationManager.AddQuickInformation(new TextObject("{=qhL5o55i}Your party is starving. You lose {MORALE_PENALTY} morale.", null), 0, null, null, "");
        });
    }

    private void Handle_NotifyAnimalsBred(MessagePayload<NotifyAnimalsBred> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyAnimalsBred(mobilePartyId, obj.What.NumberBred, obj.What.BredAnimal));
        });
    }

    private void Handle_NetworkNotifyAnimalsBred(MessagePayload<NetworkNotifyAnimalsBred> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            TextObject textObject = new TextObject("{=vl9bawa7}{COUNT} {?(COUNT > 1)}{PLURAL(ANIMAL_NAME)} are{?}{ANIMAL_NAME} is{\\?} added to your party.", null);
            textObject.SetTextVariable("COUNT", obj.What.NumberBred);
            textObject.SetTextVariable("ANIMAL_NAME", obj.What.BredAnimal.EquipmentElement.Item.Name);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
        });
    }

    private void Handle_NotifyFoundItemOnMap(MessagePayload<NotifyFoundItemOnMap> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

            network.SendAll(new NetworkNotifyFoundItemOnMap(mobilePartyId, obj.What.Count, obj.What.ItemName));
        });
    }

    private void Handle_NetworkNotifyFoundItemOnMap(MessagePayload<NetworkNotifyFoundItemOnMap> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (mobileParty != MobileParty.MainParty) return;

            TextObject textObject = new TextObject("{=vl9bawa7}{COUNT} {?(COUNT > 1)}{PLURAL(ANIMAL_NAME)} are{?}{ANIMAL_NAME} is{\\?} added to your party.", null);
            textObject.SetTextVariable("COUNT", obj.What.Count);
            textObject.SetTextVariable("ANIMAL_NAME", obj.What.ItemName);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
        });
    }

    private void Handle_NotifyKingdomInfluenceChanged(MessagePayload<NotifyKingdomInfluenceChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(obj.What.Hero, out var heroId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;
            if (!objectManager.TryGetIdWithLogging(obj.What.Clan, out var clanId)) return;

            network.SendAll(new NetworkNotifyKingdomInfluenceChanged(heroId, mobilePartyId, clanId, obj.What.GainedInfluence, obj.What.Detail));
        });
    }

    private void Handle_NetworkNotifyKingdomInfluenceChanged(MessagePayload<NetworkNotifyKingdomInfluenceChanged> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.HeroId, out var hero)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;

            if ((obj.What.Detail == GainKingdomInfluenceAction.InfluenceGainingReason.DonatePrisoners && mobileParty == MobileParty.MainParty) 
            || (obj.What.Detail == GainKingdomInfluenceAction.InfluenceGainingReason.Battle && hero == Hero.MainHero))
            {
                TextObject textObject = GameTexts.FindText("str_influence_gain_message", null);
                textObject.SetTextVariable("INFLUENCE", obj.What.GainedInfluence);
                textObject.SetTextVariable("NEW_INFLUENCE", (int)clan.Influence);
                InformationManager.DisplayMessage(new InformationMessage(textObject.ToString()));
            }
            if (obj.What.Detail == GainKingdomInfluenceAction.InfluenceGainingReason.SiegeSafePassage && hero == Hero.MainHero)
            {
                TextObject textObject2 = GameTexts.FindText("str_leave_siege_lose_influence_message", null);
                textObject2.SetTextVariable("INFLUENCE", -obj.What.GainedInfluence);
                InformationManager.DisplayMessage(new InformationMessage(textObject2.ToString()));
            }
        });
    }

    private void Handle_NetworkNotifyRemovedSupporter(MessagePayload<NetworkNotifyRemovedSupporter> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.NotableId, out var notable)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.SupportedClanId, out var supportedClan)) return;

            if (supportedClan != Clan.PlayerClan) return;

            TextObject textObject = new TextObject("{=aaOIjHeP}{NOTABLE.NAME} no longer supports your clan as your relationship deteriorated too much.", null);
            textObject.SetCharacterProperties("NOTABLE", notable.CharacterObject, false);
            InformationManager.DisplayMessage(new InformationMessage(textObject.ToString(), new Color(0f, 1f, 0f, 1f)));
        });
    }
}
