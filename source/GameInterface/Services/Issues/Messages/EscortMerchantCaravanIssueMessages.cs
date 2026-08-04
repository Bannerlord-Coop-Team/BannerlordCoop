using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.EscortMerchantCaravanIssueCreationPatch"/>) immediately
/// after <c>IssueManager.CreateNewIssue</c> creates a new
/// <see cref="EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue"/> for real. Carries the live issue
/// so <see cref="Handlers.EscortMerchantCaravanIssueHandler"/> can capture the rolled
/// <c>_companionRewardRandom</c> and replicate it - see <see cref="Interfaces.IEscortMerchantCaravanIssueInterface"/>'s
/// type doc comment.
/// </summary>
public readonly struct EscortMerchantCaravanIssueCreated : IEvent
{
    public readonly EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue Issue;

    public EscortMerchantCaravanIssueCreated(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published locally by <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/>'s Prefix when a CLIENT's
/// own (about to be blocked) call to <c>EscortMerchantCaravanIssueQuest.SpawnCaravan()</c> is intercepted.
/// </summary>
public readonly struct EscortMerchantCaravanPartySpawnRequested : IEvent
{
    public readonly Hero Owner;

    public EscortMerchantCaravanPartySpawnRequested(Hero owner)
    {
        Owner = owner;
    }
}

/// <summary>
/// Published locally on the server whenever the caravan party is genuinely created - either
/// <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/>'s Postfix (the host itself was the genuine
/// accepter, real <c>SpawnCaravan()</c> ran unmodified) or the same Postfix firing again as a side effect of
/// <see cref="Interfaces.IEscortMerchantCaravanIssueInterface.SpawnCaravanOnServer"/>'s reflective replay
/// servicing a client's forwarded <see cref="RequestEscortMerchantCaravanPartySpawn"/> - see that method's own
/// doc comment for why one Harmony Postfix covers both paths.
/// </summary>
public readonly struct EscortMerchantCaravanPartySpawned : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty CaravanParty;

    public EscortMerchantCaravanPartySpawned(Hero owner, MobileParty caravanParty)
    {
        Owner = owner;
        CaravanParty = caravanParty;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-rolled <c>_companionRewardRandom</c> for a newly created
/// Escort Merchant Caravan issue, so every client's replicated instance holds the SAME value instead of
/// independently re-rolling <c>MBRandom.RandomInt(3, 10)</c> in its own ctor.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkEscortMerchantCaravanIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int CompanionRewardRandom;

    public NetworkEscortMerchantCaravanIssueCreated(string ownerId, int companionRewardRandom)
    {
        OwnerId = ownerId;
        CompanionRewardRandom = companionRewardRandom;
    }
}

/// <summary>Client -> server: "my own live conversation just tried to spawn the escort caravan for hero X -
/// please create the real party." See <see cref="Patches.EscortMerchantCaravanPartySpawnGatePatch"/> for why
/// this needs no captured accepter-local value (unlike Caravan Ambush's party speed) - <c>SpawnCaravan()</c>
/// reads only already-shared <c>QuestGiver</c>/quest-internal state.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestEscortMerchantCaravanPartySpawn : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestEscortMerchantCaravanPartySpawn(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced caravan <see cref="MobileParty"/> id to
/// <see cref="OwnerId"/>'s <c>EscortMerchantCaravanIssueQuest._questCaravanMobileParty</c> field.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkEscortMerchantCaravanPartySpawned : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string CaravanPartyId;

    public NetworkEscortMerchantCaravanPartySpawned(string ownerId, string caravanPartyId)
    {
        OwnerId = ownerId;
        CaravanPartyId = caravanPartyId;
    }
}
