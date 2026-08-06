using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public enum VillageIssueFinalizeReason : byte
{
    IssueOnly = 0,
    QuestSuccess = 1,
    QuestCancel = 2,
    QuestFail = 3,
    QuestTimeout = 4,
    QuestBetrayal = 5,
    /// <summary>Not a genuine vanilla finalize - tells the losing side of a same-issue double-accept race to
    /// roll its own already-applied local acceptance back via <c>IssueBase.CompleteIssueWithCancel()</c>.</summary>
    RejectedAccept = 6,
    /// <summary>No <c>FinalizeMirror</c> case needed: an alternative-solution completion never has an
    /// <c>IssueQuest</c>, so the existing switch already falls through to the correct bare
    /// <c>IssueFinalized()</c> for every non-owning mirror peer.</summary>
    AlternativeSolutionSuccess = 7,
}

public readonly struct VillageIssueCreated : IEvent
{
    public readonly VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue Issue;

    public VillageIssueCreated(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        Issue = issue;
    }
}

public readonly struct VillageIssueFinalizedTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly VillageIssueFinalizeReason Reason;

    public VillageIssueFinalizedTriggered(Hero owner, VillageIssueFinalizeReason reason)
    {
        Owner = owner;
        Reason = reason;
    }
}

public readonly struct VillageIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public VillageIssueQuestAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

public readonly struct VillageIssueAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public VillageIssueAlternativeAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    /// <summary>Null when the village pays in gold instead of goods.</summary>
    [ProtoMember(3)]
    public readonly string ExchangeItemId;
    [ProtoMember(4)]
    public readonly int NumberOfRequestedItem;
    [ProtoMember(5)]
    public readonly int NumberOfExchangeItem;
    [ProtoMember(6)]
    public readonly int Payment;

    public NetworkVillageIssueCreated(
        string ownerId,
        string requestedItemId,
        string exchangeItemId,
        int numberOfRequestedItem,
        int numberOfExchangeItem,
        int payment)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        ExchangeItemId = exchangeItemId;
        NumberOfRequestedItem = numberOfRequestedItem;
        NumberOfExchangeItem = numberOfExchangeItem;
        Payment = payment;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly VillageIssueFinalizeReason Reason;

    public RequestVillageIssueRemoved(string ownerId, VillageIssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueRemoved : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly VillageIssueFinalizeReason Reason;

    public NetworkVillageIssueRemoved(string ownerId, VillageIssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueQuestAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkVillageIssueQuestAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageIssueAcceptAlternative(string ownerId)
    {
        OwnerId = ownerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueAlternativeAccepted : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkVillageIssueAlternativeAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkVillageIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
