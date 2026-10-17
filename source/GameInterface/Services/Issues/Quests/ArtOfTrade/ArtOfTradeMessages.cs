using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal sealed class ArtOfTradeProgressChanged : IEvent
{
    public Quest Quest { get; }
    public ArtOfTradeProgressChanged(Quest quest) { Quest = quest; }
}

internal sealed class ArtOfTradeJournalFinalizing : IEvent
{
    public Quest Quest { get; }
    public ArtOfTradeJournalFinalizing(Quest quest) { Quest = quest; }
}

internal sealed class ArtOfTradeIssueJournalChanged : IEvent
{
    public IssueBase Issue { get; }
    public IssueBase.IssueUpdateDetails Details { get; }
    public ArtOfTradeIssueJournalChanged(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        Issue = issue;
        Details = details;
    }
}

internal sealed class ArtOfTradePurchaseRejected : IEvent
{
    public Quest Quest { get; }
    public ArtOfTradePurchaseRejected(Quest quest) { Quest = quest; }
}

internal sealed class ArtOfTradeTroopsDeferred : IEvent
{
    public IssueBase Issue { get; }
    public ArtOfTradeTroopsDeferred(IssueBase issue) { Issue = issue; }
}

internal sealed class ArtOfTradeHourlyTick : IEvent { }

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkArtOfTradePurchaseRejected : IServerToClientCommand
{
    [ProtoMember(1)] public string IssueOwnerId { get; }
    [ProtoMember(2)] public string IssueId { get; }
    [ProtoMember(3)] public string ControllerId { get; }

    public NetworkArtOfTradePurchaseRejected(string issueOwnerId, string issueId, string controllerId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class RequestArtOfTradeProgress : ICommand
{
    [ProtoMember(1)] public string IssueOwnerId { get; }
    [ProtoMember(2)] public string IssueId { get; }
    [ProtoMember(3)] public int SoldCount { get; }
    [ProtoMember(4)] public int GatheredDenars { get; }
    [ProtoMember(5)] public int DaysPassed { get; }

    public RequestArtOfTradeProgress(string issueOwnerId, string issueId, int soldCount, int gatheredDenars, int daysPassed)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        SoldCount = soldCount;
        GatheredDenars = gatheredDenars;
        DaysPassed = daysPassed;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class ArtOfTradeJournalEntry
{
    [ProtoMember(1)] public CampaignTime Time { get; }
    [ProtoMember(2)] public TextObject Text { get; }
    [ProtoMember(3)] public TextObject Task { get; }
    [ProtoMember(4)] public int Progress { get; }
    [ProtoMember(5)] public int Range { get; }
    [ProtoMember(6)] public LogType Type { get; }

    public ArtOfTradeJournalEntry(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog CreateLog() => new JournalLog(Time, Text, Task, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
public sealed class NetworkArtOfTradeJournal : IServerToClientCommand
{
    [ProtoMember(1)] public string IssueOwnerId { get; }
    [ProtoMember(2)] public string IssueId { get; }
    [ProtoMember(3)] public string ControllerId { get; }
    [ProtoMember(4)] public ArtOfTradeJournalEntry[] Entries { get; }
    [ProtoMember(5)] public bool IsIssueJournal { get; }
    [ProtoMember(6)] public IssueBase.IssueUpdateDetails Details { get; }
    [ProtoMember(7)] public bool EffectsResolved { get; }

    public NetworkArtOfTradeJournal(string issueOwnerId, string issueId, string controllerId, ArtOfTradeJournalEntry[] entries,
        bool isIssueJournal = false, IssueBase.IssueUpdateDetails details = IssueBase.IssueUpdateDetails.None, bool effectsResolved = false)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        Entries = entries;
        IsIssueJournal = isIssueJournal;
        Details = details;
        EffectsResolved = effectsResolved;
    }
}
