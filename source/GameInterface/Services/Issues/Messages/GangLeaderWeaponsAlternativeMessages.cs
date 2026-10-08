using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

internal readonly struct GangLeaderWeaponsAlternativeUpdated : IEvent
{
    public readonly Issue Issue;
    public readonly IssueBase.IssueUpdateDetails Detail;

    public GangLeaderWeaponsAlternativeUpdated(Issue issue, IssueBase.IssueUpdateDetails detail = IssueBase.IssueUpdateDetails.None)
    {
        Issue = issue;
        Detail = detail;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkGangLeaderWeaponsAlternative : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly IssueBase.IssueUpdateDetails Detail;
    [ProtoMember(4)] public readonly bool EffectsResolved;
    [ProtoMember(5)] public readonly GangLeaderWeaponsJournalEntry[] Entries;

    public NetworkGangLeaderWeaponsAlternative(string giverId, int generation, IssueBase.IssueUpdateDetails detail,
        bool effectsResolved, GangLeaderWeaponsJournalEntry[] entries)
    {
        GiverId = giverId;
        Generation = generation;
        Detail = detail;
        EffectsResolved = effectsResolved;
        Entries = entries;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderWeaponsJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public GangLeaderWeaponsJournalEntry(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }
}
