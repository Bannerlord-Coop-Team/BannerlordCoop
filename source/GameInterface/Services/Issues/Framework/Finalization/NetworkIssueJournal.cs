using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>The server's journal, sent to the owning peer before the outcome broadcast.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueJournal : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly bool IsQuestJournal;
    [ProtoMember(4)] public readonly TextObject[] Logs;

    public NetworkIssueJournal(string issueOwnerId, string issueId, bool isQuestJournal, TextObject[] logs)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        IsQuestJournal = isQuestJournal;
        Logs = logs;
    }
}
