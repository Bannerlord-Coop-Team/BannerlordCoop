using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Mirrors the active owner's journal without running quest or reward callbacks.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestQuestJournal : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly IssueJournalEntry[] Entries;

    public RequestQuestJournal(string issueOwnerId, string issueId, IssueJournalEntry[] entries)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Entries = entries;
    }
}
