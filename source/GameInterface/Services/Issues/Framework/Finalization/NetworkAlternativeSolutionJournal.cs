using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionJournal : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly IssueBase.IssueUpdateDetails Ending;
    [ProtoMember(4)] public readonly IssueJournalEntry[] Entries;

    public NetworkAlternativeSolutionJournal(string issueOwnerId, string issueId,
        IssueBase.IssueUpdateDetails ending, IssueJournalEntry[] entries)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Ending = ending;
        Entries = entries;
    }
}
