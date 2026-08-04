using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.TheConquestOfSettlementIssueCreationPatch"/>) immediately
/// after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/> for real. Carries the live
/// issue so <see cref="Handlers.TheConquestOfSettlementIssueHandler"/> can capture the picked target settlement
/// and replicate it.
/// </summary>
public readonly struct TheConquestOfSettlementIssueCreated : IEvent
{
    public readonly TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue Issue;

    public TheConquestOfSettlementIssueCreated(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue)
    {
        Issue = issue;
    }
}

// --- Networked messages ---

/// <summary>Server -&gt; all clients: the authoritatively-picked target settlement for a newly created The
/// Conquest of a Settlement issue, so every client constructs a byte-identical instance instead of
/// independently re-rolling <c>TheConquestOfSettlementIssueBehavior.ConditionsHold</c>'s
/// <c>mBList.GetRandomElement()</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkTheConquestOfSettlementIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;

    public NetworkTheConquestOfSettlementIssueCreated(string ownerId, string targetSettlementId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
    }
}
