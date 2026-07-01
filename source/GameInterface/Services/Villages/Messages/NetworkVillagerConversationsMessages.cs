using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetPlayerVillagersInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string ConversationPartyId;

    [ProtoMember(3)]
    public readonly VillagerCampaignBehavior.PlayerInteraction Interaction;

    public NetworkSetPlayerVillagersInteraction(
        string mainHeroId,
        string conversationPartyId,
        VillagerCampaignBehavior.PlayerInteraction interaction)
    {
        MainHeroId = mainHeroId;
        ConversationPartyId = conversationPartyId;
        Interaction = interaction;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPlayerBoughtFromVillagersOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    public NetworkPlayerBoughtFromVillagersOnConsequence(
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
internal readonly struct NetworkApplyHostileVillagersInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    public NetworkApplyHostileVillagersInteraction(
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
internal readonly struct NetworkVillagersTookPrisonerOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    [ProtoMember(4)]
    public readonly ItemRosterElement[] ItemRosterElements;

    public NetworkVillagersTookPrisonerOnConsequence(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId,
        ItemRosterElement[] itemRosterElements)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
        ItemRosterElements = itemRosterElements;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkVillagersLootedLeaveOnConsequence : ICommand
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

    public NetworkVillagersLootedLeaveOnConsequence(
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
internal readonly struct NetworkVillagersSurrenderLeaveOnConsequence : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string MainPartyId;

    [ProtoMember(3)]
    public readonly string ConversationPartyId;

    [ProtoMember(4)]
    public readonly ItemRosterElement[] ItemRosterElements;

    public NetworkVillagersSurrenderLeaveOnConsequence(
        string mainHeroId,
        string mainPartyId,
        string conversationPartyId,
        ItemRosterElement[] itemRosterElements)
    {
        MainHeroId = mainHeroId;
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
        ItemRosterElements = itemRosterElements;
    }
}