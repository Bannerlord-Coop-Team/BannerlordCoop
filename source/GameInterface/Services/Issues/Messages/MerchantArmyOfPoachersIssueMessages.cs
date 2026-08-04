using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>Published on the server (from <see cref="Patches.MerchantArmyOfPoachersIssueCreationPatch"/>)
/// immediately after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue"/> for real. Carries the live
/// issue so <see cref="Handlers.MerchantArmyOfPoachersIssueHandler"/> can capture the picked village and
/// replicate it.</summary>
public readonly struct MerchantArmyOfPoachersIssueCreated : IEvent
{
    public readonly MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue Issue;

    public MerchantArmyOfPoachersIssueCreated(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>Published locally by <see cref="Patches.MerchantArmyOfPoachersPartySpawnGatePatch"/>'s Postfix when
/// an owner-CLIENT's own genuine, real <c>QuestAcceptedConsequences()</c> just ran - see that type's doc
/// comment for why the poachers-party construction itself must be forwarded to the server instead of running
/// locally.</summary>
public readonly struct MerchantArmyOfPoachersPartySpawnRequested : IEvent
{
    public readonly Hero Owner;

    public MerchantArmyOfPoachersPartySpawnRequested(Hero owner)
    {
        Owner = owner;
    }
}

/// <summary>Published locally on the server whenever <c>_poachersParty</c> is genuinely created - either
/// directly from <see cref="Patches.MerchantArmyOfPoachersPartySpawnGatePatch"/>'s Postfix (the host itself was
/// the genuine owner) or servicing a client's forwarded <see cref="RequestMerchantArmyOfPoachersPartySpawn"/>
/// via <see cref="Handlers.MerchantArmyOfPoachersIssueHandler"/>.</summary>
public readonly struct MerchantArmyOfPoachersPartySpawned : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty PoachersParty;

    public MerchantArmyOfPoachersPartySpawned(Hero owner, MobileParty poachersParty)
    {
        Owner = owner;
        PoachersParty = poachersParty;
    }
}

/// <summary>Published locally by <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/>'s
/// Prefix when an owner-CLIENT's own live game-menu option/dialogue reached <c>StartQuestBattle()</c> - see
/// that type's doc comment for why this needs server arbitration instead of running locally unchecked.</summary>
public readonly struct MerchantArmyOfPoachersBattleStartRequested : IEvent
{
    public readonly Hero Owner;

    public MerchantArmyOfPoachersBattleStartRequested(Hero owner)
    {
        Owner = owner;
    }
}

/// <summary>Published locally on the server once it validates a battle-start request (or immediately, from
/// <see cref="Patches.MerchantArmyOfPoachersBattleStartApprovalPatches"/>'s Postfix, when the host itself is
/// the genuine owner) - triggers <see cref="Handlers.MerchantArmyOfPoachersIssueHandler"/> to broadcast the
/// approval to every peer.</summary>
public readonly struct MerchantArmyOfPoachersBattleApproved : IEvent
{
    public readonly Hero Owner;

    public MerchantArmyOfPoachersBattleApproved(Hero owner)
    {
        Owner = owner;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-picked quest village for a newly created Merchant Army
/// of Poachers issue, so every client constructs a byte-identical instance instead of independently re-rolling
/// <c>Village.GetRandomElementWithPredicate</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMerchantArmyOfPoachersIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string QuestVillageId;

    public NetworkMerchantArmyOfPoachersIssueCreated(string ownerId, string questVillageId)
    {
        OwnerId = ownerId;
        QuestVillageId = questVillageId;
    }
}

/// <summary>Client -> server: "my own live accept for owner X just ran - please create the real poachers party
/// on my behalf".</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestMerchantArmyOfPoachersPartySpawn : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestMerchantArmyOfPoachersPartySpawn(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced <see cref="MobileParty"/> id to
/// <see cref="OwnerId"/>'s quest's <c>_poachersParty</c> field (same "link two already-synced objects" shape as
/// <c>NetworkGangLeaderNeedsWeaponsGuardsPartySpawned</c>).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMerchantArmyOfPoachersPartySpawned : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string PoachersPartyId;

    public NetworkMerchantArmyOfPoachersPartySpawned(string ownerId, string poachersPartyId)
    {
        OwnerId = ownerId;
        PoachersPartyId = poachersPartyId;
    }
}

/// <summary>Client -> server: "my own live game-menu option/dialogue for owner X just tried to start the
/// scripted fight - please approve it".</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMerchantArmyOfPoachersBattleStartRequest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkMerchantArmyOfPoachersBattleStartRequest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: owner X's battle start is approved - the genuine owner's own machine invokes
/// the real <c>StartQuestBattle()</c>; every other peer's mirror just flips its own local bookkeeping flag
/// (parity only - see <see cref="Interfaces.IMerchantArmyOfPoachersIssueInterface.ForceIsReadyToBeFinalized"/>'s
/// doc comment).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkMerchantArmyOfPoachersBattleApproved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkMerchantArmyOfPoachersBattleApproved(string ownerId)
    {
        OwnerId = ownerId;
    }
}
