using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestAcceptanceJournal : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly int Index;
    [ProtoMember(4)] public readonly CampaignTime Time;
    [ProtoMember(5)] public readonly TextObject Text;
    [ProtoMember(6)] public readonly TextObject TaskName;
    [ProtoMember(7)] public readonly int Progress;
    [ProtoMember(8)] public readonly int Range;
    [ProtoMember(9)] public readonly LogType Type;

    public RequestQuestAcceptanceJournal(string issueOwnerId, string issueId, int index, JournalLog log)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Index = index;
        Time = log.LogTime;
        Text = log.LogText;
        TaskName = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }
}
