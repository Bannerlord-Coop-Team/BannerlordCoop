using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct ScoutEnemyGarrisonsIssueChanged : IEvent
{
    public readonly ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue Issue;
    public readonly bool Created;

    public ScoutEnemyGarrisonsIssueChanged(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue issue, bool created)
    {
        Issue = issue;
        Created = created;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkScoutEnemyGarrisonsIssue : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string[] TargetIds;
    [ProtoMember(4)] public readonly CampaignTime DueTime;
    [ProtoMember(5)] public readonly string IssueId;

    public NetworkScoutEnemyGarrisonsIssue(string giverId, int generation, string[] targetIds, CampaignTime dueTime, string issueId)
    {
        GiverId = giverId;
        Generation = generation;
        TargetIds = targetIds;
        DueTime = dueTime;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ScoutEnemyGarrisonsAccept
{
    [ProtoMember(1)] public readonly string ControllerId;
    [ProtoMember(2)] public readonly string HeroId;
    [ProtoMember(3)] public readonly string PartyId;
    [ProtoMember(4)] public readonly string[] TargetIds;
    [ProtoMember(5)] public readonly CampaignTime DueTime;
    [ProtoMember(6)] public readonly string IssueId;

    public ScoutEnemyGarrisonsAccept(string controllerId, string heroId, string partyId, string[] targetIds, CampaignTime dueTime, string issueId)
    {
        ControllerId = controllerId;
        HeroId = heroId;
        PartyId = partyId;
        TargetIds = targetIds;
        DueTime = dueTime;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ScoutEnemyGarrisonsLog
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly int Type;

    public ScoutEnemyGarrisonsLog(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = (int)log.Type;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkScoutEnemyGarrisonsProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string QuestId;
    [ProtoMember(4)] public readonly int[] Hours;
    [ProtoMember(5)] public readonly int NeutralTargets;
    [ProtoMember(6)] public readonly int ScoutedCount;
    [ProtoMember(7)] public readonly ScoutEnemyGarrisonsLog[] Logs;
    [ProtoMember(8)] public readonly int RelationChange;

    public NetworkScoutEnemyGarrisonsProgress(string giverId, int generation, string questId, int[] hours,
        int neutralTargets, int scoutedCount, ScoutEnemyGarrisonsLog[] logs, int relationChange)
    {
        GiverId = giverId;
        Generation = generation;
        QuestId = questId;
        Hours = hours;
        NeutralTargets = neutralTargets;
        ScoutedCount = scoutedCount;
        Logs = logs;
        RelationChange = relationChange;
    }
}
