using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct GangLeaderStolenGoodsIssueCreated : IEvent
{
    public readonly GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue Issue;

    public GangLeaderStolenGoodsIssueCreated(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderStolenGoodsIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string IssueHideoutId;
    [ProtoMember(3)]
    public readonly int RandomForStolenTradeGood;
    [ProtoMember(4)]
    public readonly string CounterOfferHeroId;
    [ProtoMember(5)]
    public readonly int Generation;

    public NetworkGangLeaderStolenGoodsIssueCreated(
        string ownerId, string issueHideoutId, int randomForStolenTradeGood, string counterOfferHeroId, int generation)
    {
        OwnerId = ownerId;
        IssueHideoutId = issueHideoutId;
        RandomForStolenTradeGood = randomForStolenTradeGood;
        CounterOfferHeroId = counterOfferHeroId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct GangLeaderStolenGoodsStateSync : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly bool IsPayingForGoods;
    [ProtoMember(3)]
    public readonly bool IsFightingForGoods;
    [ProtoMember(4)]
    public readonly bool PlayerHasTheGoods;

    public GangLeaderStolenGoodsStateSync(string ownerId, bool isPayingForGoods, bool isFightingForGoods, bool playerHasTheGoods)
    {
        OwnerId = ownerId;
        IsPayingForGoods = isPayingForGoods;
        IsFightingForGoods = isFightingForGoods;
        PlayerHasTheGoods = playerHasTheGoods;
    }
}
