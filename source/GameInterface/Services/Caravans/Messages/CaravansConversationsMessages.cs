using Common.Messaging;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Caravans.Messages;

public readonly struct ChangeCaravanHomeSettlement : IEvent
{
    public readonly MobileParty ConversationParty;
    public readonly Settlement Settlement;

    public ChangeCaravanHomeSettlement(
        MobileParty conversationParty,
        Settlement settlement)
    {
        ConversationParty = conversationParty;
        Settlement = settlement;
    }
}

public readonly struct ToggleProhibitedKingdom : IEvent
{
    public readonly Hero MainHero;
    public readonly Kingdom Kingdom;
    public readonly bool KingdomAlreadyProhibited;

    public ToggleProhibitedKingdom(
        Hero mainHero,
        Kingdom kingdom,
        bool kingdomAlreadyProhibited)
    {
        MainHero = mainHero;
        Kingdom = kingdom;
        KingdomAlreadyProhibited = kingdomAlreadyProhibited;
    }
}

public readonly struct ApplyHostileCaravanInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;

    public ApplyHostileCaravanInteraction(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
    }
}

public readonly struct SetPlayerCaravanInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty ConversationParty;
    public readonly CaravansCampaignBehavior.PlayerInteraction Interaction;

    public SetPlayerCaravanInteraction(
        Hero mainHero,
        MobileParty conversationParty,
        CaravansCampaignBehavior.PlayerInteraction interaction)
    {
        MainHero = mainHero;
        ConversationParty = conversationParty;
        Interaction = interaction;
    }
}

public readonly struct UpdateTradeRumorTakenCaravans : IEvent
{
    public readonly Hero MainHero;
    public readonly Dictionary<MobileParty, CampaignTime> TradeRumorTakenCaravans;

    public UpdateTradeRumorTakenCaravans(
        Hero mainHero,
        Dictionary<MobileParty, CampaignTime> tradeRumorTakenCaravans)
    {
        MainHero = mainHero;
        TradeRumorTakenCaravans = tradeRumorTakenCaravans;
    }
}

public readonly struct CaravanLootedLeaveOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly ItemRosterElement[] ItemRosterData;
    public readonly int Amount;

    public CaravanLootedLeaveOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty,
        ItemRosterElement[] itemRosterData,
        int amount)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
        ItemRosterData = itemRosterData;
        Amount = amount;
    }
}

public readonly struct CaravanSurrenderLeaveOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly bool CaravanHasItems;

    public CaravanSurrenderLeaveOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty,
        bool caravanHasItems)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
        CaravanHasItems = caravanHasItems;
    }
}

public readonly struct CaravanTookPrisonerOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly bool CaravanHasItems;

    public CaravanTookPrisonerOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty,
        bool caravanHasItems)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
        CaravanHasItems = caravanHasItems;
    }
}
