using Common.Messaging;
using ProtoBuf;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

/// <summary>
/// Published on the server (from <see cref="Patches.RivalGangMovingInIssueCreationPatch"/>) immediately after
/// <c>IssueManager.CreateNewIssue</c> creates a new <see cref="RivalGangMovingInIssueBehavior.RivalGangMovingInIssue"/>
/// for real. Carries the live issue so <see cref="Handlers.RivalGangMovingInIssueHandler"/> can capture its
/// creation-time <c>RivalGangLeader</c> pick (<c>GetRivalGangLeader</c>'s own <c>Notables</c> walk) and
/// replicate it, so every peer's mirror is constructed against the SAME <see cref="Hero"/> reference instead of
/// each independently re-walking <c>Notables</c> - see doc/RivalGangMovingIn_Design_v2.md §2.
/// </summary>
public readonly struct RivalGangMovingInIssueCreated : IEvent
{
    public readonly RivalGangMovingInIssueBehavior.RivalGangMovingInIssue Issue;

    public RivalGangMovingInIssueCreated(RivalGangMovingInIssueBehavior.RivalGangMovingInIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Server -> all clients: the authoritatively-picked <c>RivalGangLeader</c> for a newly created Rival
/// Gang Moving In issue, so every client constructs a byte-identical instance instead of re-walking
/// <c>Notables</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRivalGangMovingInIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RivalGangLeaderId;

    public NetworkRivalGangMovingInIssueCreated(string ownerId, string rivalGangLeaderId)
    {
        OwnerId = ownerId;
        RivalGangLeaderId = rivalGangLeaderId;
    }
}

/// <summary>Outcome of a <see cref="NetworkRequestCreateRivalGangParties"/> round trip - see
/// <see cref="Handlers.RivalGangMovingInPartyCreationCoordinator"/>.</summary>
public enum RivalGangPartyCreationOutcome : byte
{
    Unresolved = 0,
    Created = 1,
    Rejected = 2,
}

/// <summary>
/// Client -> server: request the server authoritatively create BOTH the rival gang leader's and the ally
/// (quest giver's) gang leader's scripted parties + henchman Heroes for <see cref="OwnerId"/>'s ongoing Rival
/// Gang Moving In quest, in one blocking round trip - see
/// <see cref="Handlers.RivalGangMovingInPartyCreationCoordinator"/>'s doc comment for why this must be a single
/// combined blocking request rather than the fire-and-forget shape other party-spawn gates in this family use.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRequestCreateRivalGangParties : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly string OwnerId;

    public NetworkRequestCreateRivalGangParties(string requestId, string ownerId)
    {
        RequestId = requestId;
        OwnerId = ownerId;
    }
}

/// <summary>Server -> the requesting client: the resolved object-manager ids of the 2 parties + 2 henchman
/// Heroes the server just created (or an outcome explaining why it could not), replying to a single
/// <see cref="NetworkRequestCreateRivalGangParties"/> request.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRivalGangPartiesCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string RequestId;
    [ProtoMember(2)]
    public readonly RivalGangPartyCreationOutcome Outcome;
    [ProtoMember(3)]
    public readonly string RivalGangLeaderPartyId;
    [ProtoMember(4)]
    public readonly string RivalGangLeaderHenchmanHeroId;
    [ProtoMember(5)]
    public readonly string AllyGangLeaderPartyId;
    [ProtoMember(6)]
    public readonly string AllyGangLeaderHenchmanHeroId;

    public NetworkRivalGangPartiesCreated(
        string requestId,
        RivalGangPartyCreationOutcome outcome,
        string rivalGangLeaderPartyId,
        string rivalGangLeaderHenchmanHeroId,
        string allyGangLeaderPartyId,
        string allyGangLeaderHenchmanHeroId)
    {
        RequestId = requestId;
        Outcome = outcome;
        RivalGangLeaderPartyId = rivalGangLeaderPartyId;
        RivalGangLeaderHenchmanHeroId = rivalGangLeaderHenchmanHeroId;
        AllyGangLeaderPartyId = allyGangLeaderPartyId;
        AllyGangLeaderHenchmanHeroId = allyGangLeaderHenchmanHeroId;
    }
}
