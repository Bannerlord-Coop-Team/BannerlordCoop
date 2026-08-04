using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.CaravanAmbushIssueCreationPatch"/>) immediately after
/// <c>IssueManager.CreateNewIssue</c> creates a new <see cref="CaravanAmbushIssueBehavior.CaravanAmbushIssue"/>
/// for real. Carries the live issue so <see cref="Handlers.CaravanAmbushIssueHandler"/> can capture the picked
/// target settlement and replicate it.
/// </summary>
public readonly struct CaravanAmbushIssueCreated : IEvent
{
    public readonly CaravanAmbushIssueBehavior.CaravanAmbushIssue Issue;

    public CaravanAmbushIssueCreated(CaravanAmbushIssueBehavior.CaravanAmbushIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published locally by <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>'s Prefix when a CLIENT's own
/// (about to be blocked) call to <c>CaravanAmbushIssueQuest.OnQuestAccepted()</c> is intercepted -
/// <see cref="AccepterMainPartySpeed"/> is captured from THIS machine's own (genuinely correct, since this IS
/// the accepter) <c>MobileParty.MainParty.Speed</c> before the block, so the server builds the caravan's speed
/// using the accepter's own party instead of its own (possibly completely different) MainParty - see
/// <see cref="Interfaces.ICaravanAmbushIssueInterface"/>'s type doc comment.
/// </summary>
public readonly struct CaravanAmbushAcceptRequested : IEvent
{
    public readonly Hero Owner;
    public readonly float AccepterMainPartySpeed;

    public CaravanAmbushAcceptRequested(Hero owner, float accepterMainPartySpeed)
    {
        Owner = owner;
        AccepterMainPartySpeed = accepterMainPartySpeed;
    }
}

/// <summary>
/// Published locally on the server whenever the caravan/bandit parties (and reward items) are genuinely
/// created - either <see cref="Patches.CaravanAmbushPartySpawnGatePatch"/>'s Postfix (the server itself was the
/// genuine accepter, real <c>OnQuestAccepted()</c> ran unmodified) or <see cref="Handlers.CaravanAmbushIssueHandler"/>
/// after servicing a client's forwarded <see cref="RequestCaravanAmbushAccept"/> via
/// <see cref="Interfaces.ICaravanAmbushIssueInterface.CreateReplicatedAccept"/>. Consumed by the same handler
/// to broadcast <see cref="NetworkCaravanAmbushAccepted"/> to every peer.
/// </summary>
public readonly struct CaravanAmbushAccepted : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty CaravanParty;
    public readonly MobileParty BanditParty;
    public readonly List<ItemObject> RewardItems;

    public CaravanAmbushAccepted(Hero owner, MobileParty caravanParty, MobileParty banditParty, List<ItemObject> rewardItems)
    {
        Owner = owner;
        CaravanParty = caravanParty;
        BanditParty = banditParty;
        RewardItems = rewardItems;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-picked target settlement for a newly created Caravan
/// Ambush issue, so every client constructs a byte-identical instance instead of independently re-rolling
/// <c>CaravanAmbushIssueBehavior.GetTargetSettlement</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;

    public NetworkCaravanAmbushIssueCreated(string ownerId, string targetSettlementId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
    }
}

/// <summary>Client -> server: "my own live conversation just tried to accept the Caravan Ambush quest for hero
/// X - please create the real caravan/bandit parties, using MY OWN captured MainParty-derived speed, not
/// yours." See <see cref="CaravanAmbushAcceptRequested"/> for why the speed is captured client-side and
/// forwarded rather than re-derived server-side.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestCaravanAmbushAccept : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly float AccepterMainPartySpeed;

    public RequestCaravanAmbushAccept(string ownerId, float accepterMainPartySpeed)
    {
        OwnerId = ownerId;
        AccepterMainPartySpeed = accepterMainPartySpeed;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced caravan/bandit
/// <see cref="MobileParty"/> ids to <see cref="OwnerId"/>'s <c>CaravanAmbushIssueQuest._caravanParty</c>/
/// <c>_banditParty</c> fields (same "link two already-synced objects" shape as
/// <c>NetworkSmugglersPartySpawned</c>), and separately carries <see cref="RewardItemIds"/> - the server's
/// single canonical roll of the 3 reward-item <c>ItemObject</c>s, which is NOT part of any MobileParty/
/// TroopRoster and so is NOT covered by AutoRegistry sync at all.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkCaravanAmbushAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string CaravanPartyId;
    [ProtoMember(3)]
    public readonly string BanditPartyId;
    [ProtoMember(4)]
    public readonly List<string> RewardItemIds;

    public NetworkCaravanAmbushAccepted(string ownerId, string caravanPartyId, string banditPartyId, List<string> rewardItemIds)
    {
        OwnerId = ownerId;
        CaravanPartyId = caravanPartyId;
        BanditPartyId = banditPartyId;
        RewardItemIds = rewardItemIds;
    }
}
