using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.AcceptCoordination;

/// <summary>
/// Ends a rejected local accept and restores the offer when the server still has it available.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkQuestSolutionAcceptRejected : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly bool Available;

    [ProtoMember(4)]
    public readonly long DueTimeTicks;

    [ProtoMember(5)]
    public readonly bool PreviouslyTried;

    public NetworkQuestSolutionAcceptRejected(string issueOwnerId, string issueId,
        bool available = false, long dueTimeTicks = 0, bool previouslyTried = false)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Available = available;
        DueTimeTicks = dueTimeTicks;
        PreviouslyTried = previouslyTried;
    }
}
