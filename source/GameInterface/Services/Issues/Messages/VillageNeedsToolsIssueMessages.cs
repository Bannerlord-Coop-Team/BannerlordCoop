using Common.Messaging;
using ProtoBuf;
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

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    [ProtoMember(3)]
    public readonly string ExchangeItemId;
    [ProtoMember(4)]
    public readonly int NumberOfRequestedItem;
    [ProtoMember(5)]
    public readonly int NumberOfExchangeItem;
    [ProtoMember(6)]
    public readonly int Payment;
    [ProtoMember(7)]
    public readonly int Generation;

    public NetworkVillageIssueCreated(
        string ownerId,
        string requestedItemId,
        string exchangeItemId,
        int numberOfRequestedItem,
        int numberOfExchangeItem,
        int payment,
        int generation)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        ExchangeItemId = exchangeItemId;
        NumberOfRequestedItem = numberOfRequestedItem;
        NumberOfExchangeItem = numberOfExchangeItem;
        Payment = payment;
        Generation = generation;
    }
}
