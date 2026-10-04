using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ArtisanOverpricedGoodsIssueCreated : IEvent
{
    public readonly ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue Issue;

    public ArtisanOverpricedGoodsIssueCreated(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ArtisanOverpricedGoodsIssueValues
{
    [ProtoMember(1)]
    public readonly int RequestedAmount;
    [ProtoMember(2)]
    public readonly int RewardGold;
    [ProtoMember(3)]
    public readonly float Difficulty;
    [ProtoMember(4)]
    public readonly CampaignTime CreationTime;
    [ProtoMember(5)]
    public readonly CampaignTime DueTime;
    [ProtoMember(6)]
    public readonly string IssueId;

    public ArtisanOverpricedGoodsIssueValues(int requestedAmount, int rewardGold, float difficulty,
        CampaignTime creationTime, CampaignTime dueTime, string issueId)
    {
        RequestedAmount = requestedAmount;
        RewardGold = rewardGold;
        Difficulty = difficulty;
        CreationTime = creationTime;
        DueTime = dueTime;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanOverpricedGoodsIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string ItemId;
    [ProtoMember(3)]
    public readonly string CounterOfferHeroId;
    [ProtoMember(4)]
    public readonly int Generation;
    [ProtoMember(5)]
    public readonly ArtisanOverpricedGoodsIssueValues Values;

    public NetworkArtisanOverpricedGoodsIssueCreated(string ownerId, string itemId, string counterOfferHeroId,
        int generation, ArtisanOverpricedGoodsIssueValues values)
    {
        OwnerId = ownerId;
        ItemId = itemId;
        CounterOfferHeroId = counterOfferHeroId;
        Generation = generation;
        Values = values;
    }
}

public enum ArtisanOverpricedGoodsAction : byte
{
    DeliverPartial,
    DeliverFull,
    AcceptMerchantOffer,
    StartLordSolution,
    AcceptLordOffer,
    RefuseLordOffer,
}

public readonly struct ArtisanIssueOutcome : IEvent
{
    public readonly IssueBase Issue;
    public readonly IssueBase.IssueUpdateDetails Details;
    public readonly bool QuestJournal;

    public ArtisanIssueOutcome(IssueBase issue, IssueBase.IssueUpdateDetails details, bool questJournal = false)
    {
        Issue = issue;
        Details = details;
        QuestJournal = questJournal;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct ArtisanJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public ArtisanJournalEntry(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToLog() => new(Time, Text, Task, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanIssueOutcome : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly IssueBase.IssueUpdateDetails Details;
    [ProtoMember(5)] public readonly ArtisanJournalEntry[] Entries;
    [ProtoMember(6)] public readonly bool EffectsResolved;
    [ProtoMember(7)] public readonly bool QuestJournal;

    public NetworkArtisanIssueOutcome(string ownerId, int generation, string controllerId,
        IssueBase.IssueUpdateDetails details, ArtisanJournalEntry[] entries, bool effectsResolved, bool questJournal = false)
    {
        OwnerId = ownerId;
        Generation = generation;
        ControllerId = controllerId;
        Details = details;
        Entries = entries;
        EffectsResolved = effectsResolved;
        QuestJournal = questJournal;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestArtisanOverpricedGoodsAction : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;
    [ProtoMember(3)]
    public readonly int ExpectedDelivered;
    [ProtoMember(4)]
    public readonly ArtisanOverpricedGoodsAction Action;

    public RequestArtisanOverpricedGoodsAction(string ownerId, int generation, int expectedDelivered,
        ArtisanOverpricedGoodsAction action)
    {
        OwnerId = ownerId;
        Generation = generation;
        ExpectedDelivered = expectedDelivered;
        Action = action;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkArtisanOverpricedGoodsActionApplied : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int Generation;
    [ProtoMember(3)]
    public readonly string ControllerId;
    [ProtoMember(4)]
    public readonly ArtisanOverpricedGoodsAction Action;
    [ProtoMember(5)]
    public readonly int Delivered;
    [ProtoMember(6)]
    public readonly ArtisanOverpricedGoodsIssueValues Values;

    public NetworkArtisanOverpricedGoodsActionApplied(string ownerId, int generation, string controllerId,
        ArtisanOverpricedGoodsAction action, int delivered, ArtisanOverpricedGoodsIssueValues values)
    {
        OwnerId = ownerId;
        Generation = generation;
        ControllerId = controllerId;
        Action = action;
        Delivered = delivered;
        Values = values;
    }
}
