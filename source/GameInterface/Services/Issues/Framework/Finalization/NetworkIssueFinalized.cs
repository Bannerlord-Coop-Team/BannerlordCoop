using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueFinalized : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly IssueOutcome Outcome;

    [ProtoMember(4)]
    public readonly TaleWorlds.Localization.TextObject JournalLog;

    public NetworkIssueFinalized(string issueOwnerId, string issueId, IssueOutcome outcome, TaleWorlds.Localization.TextObject journalLog = null)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Outcome = outcome;
        JournalLog = journalLog;
    }
}
