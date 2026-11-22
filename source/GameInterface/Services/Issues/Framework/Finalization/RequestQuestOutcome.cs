using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestOutcome : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly IssueOutcome Outcome;

    [ProtoMember(4)]
    public readonly TextObject Log;

    public RequestQuestOutcome(string issueOwnerId, string issueId, IssueOutcome outcome, TextObject log = null)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Outcome = outcome;
        Log = log;
    }
}
