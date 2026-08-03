using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>Published on the server after a genuine <c>IssueManager.CreateNewIssue</c> creates a
/// <see cref="NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue"/>.</summary>
public readonly struct NearbyBanditBaseIssueCreated : IEvent
{
    public readonly NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue Issue;

    public NearbyBanditBaseIssueCreated(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -&gt; all clients: the picked target hideout settlement for a newly created Nearby Bandit
/// Base issue, so every client replicates the exact same target instead of independently re-searching.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkNearbyBanditBaseIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetHideoutId;

    public NetworkNearbyBanditBaseIssueCreated(string ownerId, string targetHideoutId)
    {
        OwnerId = ownerId;
        TargetHideoutId = targetHideoutId;
    }
}
