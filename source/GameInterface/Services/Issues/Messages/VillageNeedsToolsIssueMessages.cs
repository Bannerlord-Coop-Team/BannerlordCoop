using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct VillageIssueCreated : IEvent
{
    public readonly VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue Issue;

    public VillageIssueCreated(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        Issue = issue;
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
