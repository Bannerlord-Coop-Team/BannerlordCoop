using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Villages.Messages;

public readonly struct SetPlayerVillagersInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty ConversationParty;
    public readonly VillagerCampaignBehavior.PlayerInteraction Interaction;

    public SetPlayerVillagersInteraction(
        Hero mainHero,
        MobileParty conversationParty,
        VillagerCampaignBehavior.PlayerInteraction interaction)
    {
        MainHero = mainHero;
        ConversationParty = conversationParty;
        Interaction = interaction;
    }
}

public readonly struct PlayerBoughtFromVillagersOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;

    public PlayerBoughtFromVillagersOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
    }
}

public readonly struct ApplyHostileVillagersInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;

    public ApplyHostileVillagersInteraction(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
    }
}

public readonly struct VillagersTookPrisonerOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly ItemRosterElement[] ItemRosterElements;

    public VillagersTookPrisonerOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty,
        ItemRosterElement[] itemRosterElements)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
        ItemRosterElements = itemRosterElements;
    }
}


public readonly struct VillagersLootedLeaveOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly ItemRosterElement[] ItemRosterData;
    public readonly int Amount;

    public VillagersLootedLeaveOnConsequence(
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

public readonly struct VillagersSurrenderLeaveOnConsequence : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;
    public readonly MobileParty ConversationParty;
    public readonly ItemRosterElement[] ItemRosterElements;

    public VillagersSurrenderLeaveOnConsequence(
        Hero mainHero,
        MobileParty mainParty,
        MobileParty conversationParty,
        ItemRosterElement[] itemRosterElements)
    {
        MainHero = mainHero;
        MainParty = mainParty;
        ConversationParty = conversationParty;
        ItemRosterElements = itemRosterElements;
    }
}