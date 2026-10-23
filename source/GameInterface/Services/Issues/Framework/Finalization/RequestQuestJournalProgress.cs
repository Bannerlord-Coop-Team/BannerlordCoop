using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Changes only the existing owned journal stage, never its ending text or gameplay consequences.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestJournalProgress : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly int Index;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;
    [ProtoMember(7)] public readonly string TextValue;
    [ProtoMember(8)] public readonly string TaskValue;

    public RequestQuestJournalProgress(string issueOwnerId, string issueId, int index, JournalLog log)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Index = index;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
        TextValue = log.LogText?.Value;
        TaskValue = log.TaskName?.Value;
    }
}
