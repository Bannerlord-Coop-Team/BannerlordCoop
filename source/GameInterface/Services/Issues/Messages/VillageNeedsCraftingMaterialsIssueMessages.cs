using Common.Messaging;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct VillageCraftingIssueCreated : IEvent
{
    public readonly VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue Issue;

    public VillageCraftingIssueCreated(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue)
    {
        Issue = issue;
    }
}

public readonly struct VillageCraftingIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int RequestedItemAmount;
    public readonly int RewardGold;

    public VillageCraftingIssueQuestAcceptTriggered(Hero owner, string controllerId, int requestedItemAmount, int rewardGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
    }
}

public readonly struct VillageCraftingIssueAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly AlternativeSolutionVanillaState State;

    public VillageCraftingIssueAlternativeAcceptTriggered(Hero owner, string controllerId, AlternativeSolutionVanillaState state)
    {
        Owner = owner;
        ControllerId = controllerId;
        State = state;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    [ProtoMember(3)]
    public readonly int Generation;

    public NetworkVillageCraftingIssueCreated(string ownerId, string requestedItemId, int generation)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;

    public RequestVillageCraftingIssueAcceptQuest(string ownerId, int generation)
    {
        OwnerId = ownerId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueQuestAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int RequestedItemAmount;
    [ProtoMember(4)]
    public readonly int RewardGold;

    public NetworkVillageCraftingIssueQuestAccepted(string ownerId, string ownerControllerId, int requestedItemAmount, int rewardGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly CampaignTime ReturnTime;
    [ProtoMember(3)]
    public readonly CampaignTime EffectClearTime;
    [ProtoMember(4)]
    public readonly float FailureChance;
    [ProtoMember(5)]
    public readonly int CasualtyCount;
    [ProtoMember(6)]
    public readonly float TotalTroopXpAmount;
    [ProtoMember(7)]
    public readonly string CompanionRewardSkillId;
    [ProtoMember(8)]
    public readonly int Generation;
    [ProtoMember(9)]
    public readonly TroopRosterData SentTroops;

    public RequestVillageCraftingIssueAcceptAlternative(string ownerId, AlternativeSolutionVanillaState state, int generation, TroopRosterData sentTroops)
    {
        OwnerId = ownerId;
        ReturnTime = state.ReturnTime;
        EffectClearTime = state.EffectClearTime;
        FailureChance = state.FailureChance;
        CasualtyCount = state.CasualtyCount;
        TotalTroopXpAmount = state.TotalTroopXpAmount;
        CompanionRewardSkillId = state.CompanionRewardSkillId;
        Generation = generation;
        SentTroops = sentTroops;
    }

    public AlternativeSolutionVanillaState ToState() =>
        new(ReturnTime, EffectClearTime, FailureChance, CasualtyCount, TotalTroopXpAmount, CompanionRewardSkillId);
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueAlternativeAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly CampaignTime ReturnTime;
    [ProtoMember(4)]
    public readonly CampaignTime EffectClearTime;
    [ProtoMember(5)]
    public readonly float FailureChance;
    [ProtoMember(6)]
    public readonly int CasualtyCount;
    [ProtoMember(7)]
    public readonly float TotalTroopXpAmount;
    [ProtoMember(8)]
    public readonly string CompanionRewardSkillId;

    public NetworkVillageCraftingIssueAlternativeAccepted(string ownerId, string ownerControllerId, AlternativeSolutionVanillaState state)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        ReturnTime = state.ReturnTime;
        EffectClearTime = state.EffectClearTime;
        FailureChance = state.FailureChance;
        CasualtyCount = state.CasualtyCount;
        TotalTroopXpAmount = state.TotalTroopXpAmount;
        CompanionRewardSkillId = state.CompanionRewardSkillId;
    }

    public AlternativeSolutionVanillaState ToState() =>
        new(ReturnTime, EffectClearTime, FailureChance, CasualtyCount, TotalTroopXpAmount, CompanionRewardSkillId);
}

public readonly struct VillageCraftingIssueAlternativeSolutionCompletionRequested : IEvent
{
    public readonly Hero Owner;

    public VillageCraftingIssueAlternativeSolutionCompletionRequested(Hero owner)
    {
        Owner = owner;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageCraftingIssueAlternativeSolutionCompletion : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageCraftingIssueAlternativeSolutionCompletion(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageCraftingIssueAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkVillageCraftingIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
