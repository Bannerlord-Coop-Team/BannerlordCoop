using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue"/>.</summary>
public readonly struct CapturedByBountyHuntersIssueCreated : IEvent
{
    public readonly CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue Issue;

    public CapturedByBountyHuntersIssueCreated(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the picked hideout settlement for a newly created Captured by Bounty
/// Hunters issue, so every client replicates the exact same target instead of independently re-searching.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCapturedByBountyHuntersIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string HideoutId;

    public NetworkCapturedByBountyHuntersIssueCreated(string ownerId, string hideoutId)
    {
        OwnerId = ownerId;
        HideoutId = hideoutId;
    }
}
