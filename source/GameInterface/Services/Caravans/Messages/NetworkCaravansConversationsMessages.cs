using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Caravans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangeCaravanHomeSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string ConversationPartyId;

    [ProtoMember(2)]
    public readonly string SettlementId;

    public NetworkChangeCaravanHomeSettlement(
        string conversationPartyId,
        string settlementId)
    {
        ConversationPartyId = conversationPartyId;
        SettlementId = settlementId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkToggleProhibitedKingdom : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string KingdomId;

    [ProtoMember(3)]
    public readonly bool KingdomAlreadyProhibited;

    public NetworkToggleProhibitedKingdom(
        string mainHeroId,
        string kingdomId,
        bool kingdomAlreadyProhibited)
    {
        MainHeroId = mainHeroId;
        KingdomId = kingdomId;
        KingdomAlreadyProhibited = kingdomAlreadyProhibited;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkApplyHostileCaravanInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    public NetworkApplyHostileCaravanInteraction(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPlayerCaravanInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string ConversationPartyId;

    [ProtoMember(3)]
    public readonly CaravansCampaignBehavior.PlayerInteraction Interaction;

    public NetworkSetPlayerCaravanInteraction(
        string mainHeroId,
        string conversationPartyId,
        CaravansCampaignBehavior.PlayerInteraction interaction)
    {
        MainHeroId = mainHeroId;
        ConversationPartyId = conversationPartyId;
        Interaction = interaction;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkUpdateTradeRumorTakenCaravans : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly Dictionary<string, long> TradeRumorTakenCaravansIds;

    public NetworkUpdateTradeRumorTakenCaravans(
        string mainHeroId,
        Dictionary<string, long> tradeRumorTakenCaravansIds)
    {
        MainHeroId = mainHeroId;
        TradeRumorTakenCaravansIds = tradeRumorTakenCaravansIds;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravanLootedLeaveOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    [ProtoMember(4)]
    public readonly ItemRosterElement[] ItemRosterData;

    [ProtoMember(5)]
    public readonly int Amount;

    public NetworkCaravanLootedLeaveOnConsequence(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId,
        ItemRosterElement[] itemRosterData,
        int amount)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
        ItemRosterData = itemRosterData;
        Amount = amount;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravanSurrenderLeaveOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    [ProtoMember(4)]
    public readonly bool CaravanHasItems;

    [ProtoMember(5)]
    public readonly ItemRosterElement[] ItemRosterElements;

    public NetworkCaravanSurrenderLeaveOnConsequence(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId,
        bool caravanHasItems,
        ItemRosterElement[] itemRosterElements)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
        CaravanHasItems = caravanHasItems;
        ItemRosterElements = itemRosterElements;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCaravanTookPrisonerOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    [ProtoMember(4)]
    public readonly bool CaravanHasItems;

    [ProtoMember(5)]
    public readonly ItemRosterElement[] ItemRosterElements;

    public NetworkCaravanTookPrisonerOnConsequence(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId,
        bool caravanHasItems,
        ItemRosterElement[] itemRosterElements)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
        CaravanHasItems = caravanHasItems;
        ItemRosterElements = itemRosterElements;
    }
}
