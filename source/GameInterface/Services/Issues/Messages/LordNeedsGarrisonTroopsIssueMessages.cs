using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Creation: server rolls once, broadcasts the resolved needed-troop-type ---

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue"/>, carrying the live
/// issue so <see cref="Handlers.LordNeedsGarrisonTroopsIssueHandler"/> can capture its one creation-time-rolled
/// field.</summary>
public readonly struct LordNeedsGarrisonTroopsIssueCreated : IEvent
{
    public readonly LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue Issue;

    public LordNeedsGarrisonTroopsIssueCreated(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the authoritatively-rolled needed troop type, plus the (already
/// deterministic) settlement id, for a newly created Lord Needs Garrison Troops issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordNeedsGarrisonTroopsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string SettlementId;
    [ProtoMember(3)]
    public readonly string NeededTroopTypeId;

    public NetworkLordNeedsGarrisonTroopsIssueCreated(string ownerId, string settlementId, string neededTroopTypeId)
    {
        OwnerId = ownerId;
        SettlementId = settlementId;
        NeededTroopTypeId = neededTroopTypeId;
    }
}

// --- Acceptance: the accepting machine already applied it locally for real; the server arbitrates a
// same-issue double-accept race and confirms/rejects it, resolving and broadcasting the authoritative
// required-troop-amount/reward every peer force-corrects to. ---

/// <summary>Published from <see cref="Patches.LordNeedsGarrisonTroopsAcceptancePatches"/> whenever
/// <c>IssueManager.StartIssueQuest</c> genuinely runs for this issue type. Carries the accepting machine's
/// captured <c>_requestedTroopAmount</c>/<c>_rewardGold</c> - see
/// <see cref="Interfaces.ILordNeedsGarrisonTroopsIssueInterface"/>'s doc comment.</summary>
public readonly struct LordNeedsGarrisonTroopsIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;
    public readonly int RequestedTroopAmount;
    public readonly int RewardGold;

    public LordNeedsGarrisonTroopsIssueQuestAcceptTriggered(Hero owner, string controllerId, int requestedTroopAmount, int rewardGold)
    {
        Owner = owner;
        ControllerId = controllerId;
        RequestedTroopAmount = requestedTroopAmount;
        RewardGold = rewardGold;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLordNeedsGarrisonTroopsIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestLordNeedsGarrisonTroopsIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordNeedsGarrisonTroopsIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;
    [ProtoMember(3)]
    public readonly int RequestedTroopAmount;
    [ProtoMember(4)]
    public readonly int RewardGold;

    public NetworkLordNeedsGarrisonTroopsIssueQuestAccepted(string ownerId, string ownerControllerId, int requestedTroopAmount, int rewardGold)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
        RequestedTroopAmount = requestedTroopAmount;
        RewardGold = rewardGold;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordNeedsGarrisonTroopsIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkLordNeedsGarrisonTroopsIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
