using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct LordNeedsHorsesIssueCreated : IEvent
{
    public readonly LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue Issue;

    public LordNeedsHorsesIssueCreated(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLordNeedsHorsesIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string MountItemId;
    [ProtoMember(3)]
    public readonly int NumMountsToBeDelivered;
    [ProtoMember(4)]
    public readonly int MountValuePerUnit;
    [ProtoMember(5)] public readonly int Generation;
    [ProtoMember(6)] public readonly CampaignTime DueTime;
    [ProtoMember(7)] public readonly string IssueId;
    [ProtoMember(8)] public readonly int NextIssueIndex;
    [ProtoMember(9)] public readonly float DifficultyMultiplier;

    public NetworkLordNeedsHorsesIssueCreated(string ownerId, string mountItemId, int numMountsToBeDelivered, int mountValuePerUnit,
        int generation, CampaignTime dueTime, string issueId, int nextIssueIndex, float difficultyMultiplier)
    {
        OwnerId = ownerId;
        MountItemId = mountItemId;
        NumMountsToBeDelivered = numMountsToBeDelivered;
        MountValuePerUnit = mountValuePerUnit;
        Generation = generation;
        DueTime = dueTime;
        IssueId = issueId;
        NextIssueIndex = nextIssueIndex;
        DifficultyMultiplier = difficultyMultiplier;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct LordNeedsHorsesAcceptFields
{
    [ProtoMember(1)] public readonly string PlayerHeroId;
    [ProtoMember(2)] public readonly string PlayerPartyId;
    [ProtoMember(3)] public readonly CampaignTime DueTime;
    [ProtoMember(4)] public readonly NetworkLordNeedsHorsesJournal Journal;

    public LordNeedsHorsesAcceptFields(string playerHeroId, string playerPartyId, CampaignTime dueTime,
        NetworkLordNeedsHorsesJournal journal)
    {
        PlayerHeroId = playerHeroId;
        PlayerPartyId = playerPartyId;
        DueTime = dueTime;
        Journal = journal;
    }
}

internal readonly struct LordNeedsHorsesJournalChanged : IEvent
{
    public readonly LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue Issue;
    public readonly IssueBase.IssueUpdateDetails Status;
    public LordNeedsHorsesJournalChanged(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue issue,
        IssueBase.IssueUpdateDetails status = IssueBase.IssueUpdateDetails.None)
    {
        Issue = issue;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLordNeedsHorsesJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly LordNeedsHorsesJournalEntry[] Entries;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly bool IsQuest;
    [ProtoMember(6)] public readonly int IssueStatus;
    [ProtoMember(7)] public readonly float DifficultyMultiplier;
    [ProtoMember(8)] public readonly bool EffectsResolved;

    public NetworkLordNeedsHorsesJournal(string ownerId, int generation, LordNeedsHorsesJournalEntry[] entries,
        int progress, bool isQuest, int issueStatus, float difficultyMultiplier, bool effectsResolved)
    {
        OwnerId = ownerId;
        Generation = generation;
        Entries = entries;
        Progress = progress;
        IsQuest = isQuest;
        IssueStatus = issueStatus;
        DifficultyMultiplier = difficultyMultiplier;
        EffectsResolved = effectsResolved;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct LordNeedsHorsesJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly byte[] Text;
    [ProtoMember(3)] public readonly byte[] Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly int Type;

    public LordNeedsHorsesJournalEntry(CampaignTime time, byte[] text, byte[] task, int progress, int range, int type)
    {
        Time = time;
        Text = text;
        Task = task;
        Progress = progress;
        Range = range;
        Type = type;
    }
}

internal readonly struct LordNeedsHorsesTraitProgressChanged : IEvent
{
    public readonly Hero Hero;
    public readonly int Progress;
    public LordNeedsHorsesTraitProgressChanged(Hero hero, int progress)
    {
        Hero = hero;
        Progress = progress;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkLordNeedsHorsesTraitProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string HeroId;
    [ProtoMember(2)] public readonly int Progress;
    public NetworkLordNeedsHorsesTraitProgress(string heroId, int progress)
    {
        HeroId = heroId;
        Progress = progress;
    }
}
