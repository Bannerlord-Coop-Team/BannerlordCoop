using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>Published on the server (from <see cref="Patches.LandLordCompanyOfTroubleIssueCreationPatch"/>)
/// immediately after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue"/> for real. Carries the live
/// issue so <see cref="Handlers.LandLordCompanyOfTroubleIssueHandler"/> can broadcast it - no extra field
/// needed, same shape as <see cref="GangLeaderNeedsWeaponsIssueCreated"/> (the Issue ctor takes only the
/// owner).</summary>
public readonly struct LandLordCompanyOfTroubleIssueCreated : IEvent
{
    public readonly LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue Issue;

    public LandLordCompanyOfTroubleIssueCreated(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Published locally by <see cref="Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch"/>'s
/// Prefix when an owner-CLIENT's own live dialogue reached <c>CreateCompanyEnemyParty()</c> - carries the
/// trigger-time capture (the accepter's OWN <c>MobileParty.MainParty.Position</c> and freshly-refreshed
/// embedded-troop count) so the server can build the real hostile party using the genuine owner's state instead
/// of its own. See <see cref="Interfaces.ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment for why
/// this is captured HERE, at trigger time, rather than at accept time.</summary>
public readonly struct LandLordCompanyOfTroubleEnemyPartySpawnRequested : IEvent
{
    public readonly Hero Owner;
    public readonly CampaignVec2 Position;
    public readonly int TroopCount;

    public LandLordCompanyOfTroubleEnemyPartySpawnRequested(Hero owner, CampaignVec2 position, int troopCount)
    {
        Owner = owner;
        Position = position;
        TroopCount = troopCount;
    }
}

/// <summary>Published locally on the server whenever <c>_companyOfTroubleParty</c> is genuinely created - either
/// <see cref="Patches.LandLordCompanyOfTroubleEnemyPartySpawnGatePatch"/>'s Postfix on
/// <c>CreateCompanyEnemyParty</c> (the host itself was the genuine owner, real vanilla body ran unmodified) or
/// servicing a client's forwarded <see cref="RequestLandLordCompanyOfTroubleEnemyPartySpawn"/> via
/// <see cref="Interfaces.ILandLordCompanyOfTroubleIssueInterface.CreateCompanyEnemyPartyOnServer"/>.</summary>
public readonly struct LandLordCompanyOfTroubleEnemyPartySpawned : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty CompanyOfTroubleParty;

    public LandLordCompanyOfTroubleEnemyPartySpawned(Hero owner, MobileParty companyOfTroubleParty)
    {
        Owner = owner;
        CompanyOfTroubleParty = companyOfTroubleParty;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: a new Company of Trouble issue was created for <see cref="OwnerId"/> - every
/// client independently, deterministically constructs its own byte-identical mirror (the Issue ctor takes only
/// the owner - see <see cref="Interfaces.ILandLordCompanyOfTroubleIssueInterface"/>'s type doc comment).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandLordCompanyOfTroubleIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkLandLordCompanyOfTroubleIssueCreated(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Client -> server: "my own live dialogue for owner X just turned the mercenaries hostile - here is
/// MY OWN MainParty's position and freshly-refreshed embedded-troop count at that exact moment; please build the
/// real hostile party on my behalf using THESE values, not your own".</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly CampaignVec2 Position;
    [ProtoMember(3)]
    public readonly int TroopCount;

    public NetworkLandLordCompanyOfTroubleEnemyPartySpawnRequest(string ownerId, CampaignVec2 position, int troopCount)
    {
        OwnerId = ownerId;
        Position = position;
        TroopCount = troopCount;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced <see cref="MobileParty"/> id to
/// <see cref="OwnerId"/>'s quest's <c>_companyOfTroubleParty</c> field (same "link two already-synced objects"
/// shape as <c>NetworkMerchantArmyOfPoachersPartySpawned</c>) - also the signal every peer (including the true
/// owner) uses to flip <c>_battleWillStart</c>, folding this quest's "battle-start-approval" into the same
/// round trip as the party spawn (see the type doc comment for why - there is no separately-callable
/// <c>StartQuestBattle()</c>-shaped method here to gate independently).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLandLordCompanyOfTroubleEnemyPartySpawned : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string CompanyOfTroublePartyId;

    public NetworkLandLordCompanyOfTroubleEnemyPartySpawned(string ownerId, string companyOfTroublePartyId)
    {
        OwnerId = ownerId;
        CompanyOfTroublePartyId = companyOfTroublePartyId;
    }
}
