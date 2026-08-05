using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>Published on the server (from <see cref="Patches.GangLeaderNeedsWeaponsIssueCreationPatch"/>)
/// immediately after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue"/> for real. Carries the live
/// issue so <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/> can broadcast it - no extra field needed,
/// see <see cref="Interfaces.IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment.</summary>
public readonly struct GangLeaderNeedsWeaponsIssueCreated : IEvent
{
    public readonly GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue Issue;

    public GangLeaderNeedsWeaponsIssueCreated(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Published locally by <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>'s Prefix
/// when an owner-CLIENT's own (about to be blocked) <c>OnSettlementEnter</c> would have created
/// <c>_guardsParty</c> - see that type's doc comment for why no accepter-derived value needs to be captured
/// here at all.</summary>
public readonly struct GangLeaderNeedsWeaponsGuardsPartySpawnRequested : IEvent
{
    public readonly Hero Owner;

    public GangLeaderNeedsWeaponsGuardsPartySpawnRequested(Hero owner)
    {
        Owner = owner;
    }
}

/// <summary>Published locally on the server whenever <c>_guardsParty</c> is genuinely created - either
/// <see cref="Patches.GangLeaderNeedsWeaponsGuardsPartySpawnGatePatch"/>'s Postfix on <c>CreateGuardsParty</c>
/// (the host itself was the genuine owner, real <c>OnSettlementEnter</c> ran unmodified) or servicing a client's
/// forwarded <see cref="RequestGangLeaderNeedsWeaponsGuardsPartySpawn"/> via
/// <see cref="Interfaces.IGangLeaderNeedsWeaponsIssueInterface.CreateGuardsPartyOnServer"/> - both paths reach
/// the SAME real, Harmony-postfixed private method, so one Postfix covers both.</summary>
public readonly struct GangLeaderNeedsWeaponsGuardsPartySpawned : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty GuardsParty;

    public GangLeaderNeedsWeaponsGuardsPartySpawned(Hero owner, MobileParty guardsParty)
    {
        Owner = owner;
        GuardsParty = guardsParty;
    }
}

/// <summary>Published locally by <see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/>'s Prefix
/// when an owner-CLIENT's own live dialogue reached <c>StartFight()</c> - see that type's doc comment for why
/// this needs server arbitration instead of running locally unchecked.</summary>
public readonly struct GangLeaderNeedsWeaponsBattleStartRequested : IEvent
{
    public readonly Hero Owner;

    public GangLeaderNeedsWeaponsBattleStartRequested(Hero owner)
    {
        Owner = owner;
    }
}

/// <summary>Published locally on the server once it validates a battle-start request (or immediately, from
/// <see cref="Patches.GangLeaderNeedsWeaponsBattleStartApprovalPatches"/>'s Postfix, when the host itself is the
/// genuine owner) - triggers <see cref="Handlers.GangLeaderNeedsWeaponsIssueHandler"/> to broadcast the
/// approval to every peer.</summary>
public readonly struct GangLeaderNeedsWeaponsBattleApproved : IEvent
{
    public readonly Hero Owner;

    public GangLeaderNeedsWeaponsBattleApproved(Hero owner)
    {
        Owner = owner;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: a new Gang Leader Needs Weapons issue was created for <see cref="OwnerId"/> -
/// every client independently, deterministically constructs its own byte-identical mirror (see
/// <see cref="Interfaces.IGangLeaderNeedsWeaponsIssueInterface"/>'s type doc comment for why no other field
/// needs to travel with this message).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderNeedsWeaponsIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangLeaderNeedsWeaponsIssueCreated(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Client -> server: "my own live settlement-entry for owner X's quest just tried to create the guards
/// party - please create the real one, deterministically, on my behalf".</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangLeaderNeedsWeaponsGuardsPartySpawnRequest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced <see cref="MobileParty"/> id to
/// <see cref="OwnerId"/>'s quest's <c>_guardsParty</c> field (same "link two already-synced objects" shape as
/// <c>NetworkSmugglersPartySpawned</c>/<c>NetworkCaravanAmbushAccepted</c>).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderNeedsWeaponsGuardsPartySpawned : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string GuardsPartyId;

    public NetworkGangLeaderNeedsWeaponsGuardsPartySpawned(string ownerId, string guardsPartyId)
    {
        OwnerId = ownerId;
        GuardsPartyId = guardsPartyId;
    }
}

/// <summary>Client -> server: "my own live guard dialogue for owner X just tried to start the scripted fight -
/// please approve it".</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderNeedsWeaponsBattleStartRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangLeaderNeedsWeaponsBattleStartRequest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: owner X's battle start is approved - the genuine owner's own machine invokes
/// the real <c>StartFight()</c>; every other peer's mirror just flips its own local bookkeeping flag (parity
/// only - see <see cref="Interfaces.IGangLeaderNeedsWeaponsIssueInterface.ForceCheckForBattleResult"/>'s doc
/// comment).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkGangLeaderNeedsWeaponsBattleApproved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkGangLeaderNeedsWeaponsBattleApproved(string ownerId)
    {
        OwnerId = ownerId;
    }
}
