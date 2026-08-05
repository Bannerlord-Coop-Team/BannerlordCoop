using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Messages;

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.SmugglersIssueCreationPatch"/>) immediately after
/// <c>IssueManager.CreateNewIssue</c> creates a new <see cref="SmugglersIssueBehavior.SmugglersIssue"/> for
/// real. Carries the live issue so <see cref="Handlers.SmugglersIssueHandler"/> can capture the picked
/// target/origin settlement pair and replicate it.
/// </summary>
public readonly struct SmugglersIssueCreated : IEvent
{
    public readonly SmugglersIssueBehavior.SmugglersIssue Issue;

    public SmugglersIssueCreated(SmugglersIssueBehavior.SmugglersIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published locally by <see cref="Patches.SmugglersPartySpawnGatePatch"/>'s Prefix when a CLIENT's own
/// (about to be blocked) call to <c>SmugglersIssueQuest.CreateSmugglerParty()</c> is intercepted -
/// <see cref="DesiredMenCount"/>/<see cref="CustomPartyBaseSpeed"/> are captured from THIS machine's own
/// (genuinely correct, since this IS the accepter) <c>MobileParty.MainParty</c> before the block, so the
/// server creates the real party using the accepter's own composition instead of its own (possibly
/// completely different) MainParty - see <see cref="Interfaces.ISmugglersIssueInterface"/>'s type doc comment.
/// </summary>
public readonly struct SmugglersPartySpawnRequested : IEvent
{
    public readonly Hero Owner;
    public readonly int DesiredMenCount;
    public readonly float CustomPartyBaseSpeed;

    public SmugglersPartySpawnRequested(Hero owner, int desiredMenCount, float customPartyBaseSpeed)
    {
        Owner = owner;
        DesiredMenCount = desiredMenCount;
        CustomPartyBaseSpeed = customPartyBaseSpeed;
    }
}

/// <summary>
/// Published locally on the server whenever a real smuggler <see cref="MobileParty"/> is genuinely created -
/// either <see cref="Patches.SmugglersPartySpawnGatePatch"/>'s Postfix (the server itself was the genuine
/// accepter, real <c>CreateSmugglerParty()</c> ran unmodified) or <see cref="Handlers.SmugglersIssueHandler"/>
/// after servicing a client's forwarded <see cref="RequestSmugglerPartySpawn"/> via
/// <see cref="Interfaces.ISmugglersIssueInterface.CreateReplicatedSmugglerParty"/>. Consumed by the same
/// handler to broadcast <see cref="NetworkSmugglersPartySpawned"/> to every peer.
/// </summary>
public readonly struct SmugglersPartySpawned : IEvent
{
    public readonly Hero Owner;
    public readonly MobileParty Party;

    public SmugglersPartySpawned(Hero owner, MobileParty party)
    {
        Owner = owner;
        Party = party;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-picked target/origin settlement pair for a newly
/// created Smugglers issue, so every client constructs a byte-identical instance instead of independently
/// re-rolling <c>ConditionsHold</c>'s <c>GetRandomElementInefficiently()</c> locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSmugglersIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string OriginSettlementId;

    public NetworkSmugglersIssueCreated(string ownerId, string targetSettlementId, string originSettlementId)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        OriginSettlementId = originSettlementId;
    }
}

/// <summary>Client -> server: "my own live conversation just tried to spawn the smuggler party for hero X -
/// please create the real one, using MY OWN captured MainParty-derived composition, not yours." See
/// <see cref="SmugglersPartySpawnRequested"/> for why the composition is captured client-side and forwarded
/// rather than re-derived server-side.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestSmugglerPartySpawn : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly int DesiredMenCount;
    [ProtoMember(3)]
    public readonly float CustomPartyBaseSpeed;

    public RequestSmugglerPartySpawn(string ownerId, int desiredMenCount, float customPartyBaseSpeed)
    {
        OwnerId = ownerId;
        DesiredMenCount = desiredMenCount;
        CustomPartyBaseSpeed = customPartyBaseSpeed;
    }
}

/// <summary>Server -> all clients: links the already-AutoRegistry-synced <see cref="MobileParty"/>
/// <see cref="PartyId"/> to <see cref="OwnerId"/>'s <c>SmugglersIssueQuest._smugglerParty</c> field - same
/// "link two already-synced objects" shape as the existing
/// <c>PartyComponentMobilePartyUpdated</c>/<c>NetworkPartyComponentMobilePartyUpdated</c> precedent. The party's
/// own contents (roster, position, component state) are NOT carried here - they already replicated via the
/// normal <c>MobilePartyRegistry</c> AutoRegistry sync, which always runs first (nested inside the same
/// synchronous <c>CreateSmugglerParty</c>/<c>CreateReplicatedSmugglerParty</c> call this message is published
/// right after).</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSmugglersPartySpawned : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string PartyId;

    public NetworkSmugglersPartySpawned(string ownerId, string partyId)
    {
        OwnerId = ownerId;
        PartyId = partyId;
    }
}
