using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

/// <summary>
/// Why an <see cref="IssueBase.IssueFinalized"/> broadcast is being mirrored - lets a peer applying a
/// received (or server-replayed) finalize call the exact matching <c>QuestBase.CompleteQuestWithXxx</c>/
/// <c>IssueBase.CompleteIssueWithXxx</c> instead of the bare, quest-orphaning <c>IssueFinalized()</c> call
/// the first pass used unconditionally - see <see cref="Patches.IssueFinalizedPatches"/> and
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface.FinalizeMirror"/>.
/// </summary>
public enum VillageIssueFinalizeReason : byte
{
    /// <summary>No active <see cref="IssueBase.IssueQuest"/> to cascade through (timed out / stay-alive
    /// conditions failed / finished by an AI lord, all only reachable while <c>IsOngoingWithoutQuest</c>) -
    /// a bare <c>IssueFinalized()</c> is exactly what vanilla itself does for these.</summary>
    IssueOnly = 0,
    QuestSuccess = 1,
    QuestCancel = 2,
    QuestFail = 3,
    QuestTimeout = 4,
    QuestBetrayal = 5,
    /// <summary>Not a genuine vanilla finalize at all - a synthetic instruction telling the losing side of a
    /// same-issue double-accept race to roll its own already-applied (optimistic) local acceptance back via
    /// <c>IssueBase.CompleteIssueWithCancel()</c>. See <see cref="Patches.IssueAcceptancePatches"/>.</summary>
    RejectedAccept = 6,
    /// <summary>Bug 2 fix: the owning peer's own <c>HourlyTickEvent</c> listener genuinely triggered
    /// <c>IssueBase.CompleteIssueWithAlternativeSolution()</c> for real (see
    /// <see cref="Interfaces.VillageNeedsToolsIssueInterface.TryTriggerOwnedAlternativeSolutionCompletion"/>
    /// and <see cref="Patches.VillageNeedsToolsAlternativeSolutionCompletionPatches"/>). Purely a label for
    /// legible logs/telemetry - <c>FinalizeMirror</c> needs no new <c>case</c> for it: an alternative-solution
    /// completion never has an <c>IssueQuest</c>, so its existing switch already falls through to the
    /// correct bare <c>IssueFinalized()</c> (teardown only, no reward, no troop mutation) for every
    /// non-owning mirror peer. A "Fail" variant is deliberately not added: unreachable for this issue type
    /// (see the deterministic-success note on <c>TryTriggerOwnedAlternativeSolutionCompletion</c>) - adding
    /// it now would be speculative dead code for a hypothetical future issue type, so it's flagged here
    /// rather than built.</summary>
    AlternativeSolutionSuccess = 7,
}

// --- Local events ---

/// <summary>
/// Published on the server (from <see cref="Patches.IssueManagerCreateNewIssuePatches"/>) immediately
/// after <c>IssueManager.CreateNewIssue</c> creates a new <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/>
/// for real. Carries the live issue so <see cref="Handlers.VillageNeedsToolsIssueHandler"/> can capture its
/// rolled fields (requested/exchange item, quantities, payment) and replicate them.
/// </summary>
public readonly struct VillageIssueCreated : IEvent
{
    public readonly VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue Issue;

    public VillageIssueCreated(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        Issue = issue;
    }
}

/// <summary>
/// Published (on whichever machine reaches it - the server for an ambient-tick timeout, or the one client
/// in the quest-turn-in conversation for a success) from <see cref="Patches.IssueFinalizedPatches"/>
/// whenever a <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/>'s <c>IssueFinalized</c>
/// runs for real (success, timeout, cancel, betrayal, fail, ...) rather than as a mirrored replay of a
/// received network message. See <see cref="Handlers.VillageNeedsToolsIssueHandler"/> for the client/server
/// routing this drives.
/// </summary>
public readonly struct VillageIssueFinalizedTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly VillageIssueFinalizeReason Reason;

    public VillageIssueFinalizedTriggered(Hero owner, VillageIssueFinalizeReason reason)
    {
        Owner = owner;
        Reason = reason;
    }
}

/// <summary>
/// Published from <see cref="Patches.IssueAcceptancePatches"/> whenever <c>IssueManager.StartIssueQuest</c>
/// genuinely runs (the accepting player's own live conversation, on whichever machine that is - not a
/// mirrored replay of a received broadcast). <see cref="ControllerId"/> is the accepting machine's own
/// <c>IControllerIdProvider.ControllerId</c> at the moment of the genuine accept - see
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership"/>.
/// </summary>
public readonly struct VillageIssueQuestAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public VillageIssueQuestAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

/// <summary>
/// Published from <see cref="Patches.IssueAcceptancePatches"/> whenever <c>IssueBase.StartIssueWithAlternativeSolution</c>
/// genuinely runs. See <see cref="VillageIssueQuestAcceptTriggered"/> for what <see cref="ControllerId"/> is.
/// </summary>
public readonly struct VillageIssueAlternativeAcceptTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public VillageIssueAlternativeAcceptTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

// --- Networked messages ---

/// <summary>Server -> all clients: the authoritatively-rolled terms of a newly created Village Needs
/// Tools issue, so every client constructs a byte-identical instance instead of re-rolling locally.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueCreated : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string RequestedItemId;
    /// <summary>Null when the village pays in gold instead of goods (see the ctor: Village.Hearth &gt;= 300).</summary>
    [ProtoMember(3)]
    public readonly string ExchangeItemId;
    [ProtoMember(4)]
    public readonly int NumberOfRequestedItem;
    [ProtoMember(5)]
    public readonly int NumberOfExchangeItem;
    [ProtoMember(6)]
    public readonly int Payment;

    public NetworkVillageIssueCreated(
        string ownerId,
        string requestedItemId,
        string exchangeItemId,
        int numberOfRequestedItem,
        int numberOfExchangeItem,
        int payment)
    {
        OwnerId = ownerId;
        RequestedItemId = requestedItemId;
        ExchangeItemId = exchangeItemId;
        NumberOfRequestedItem = numberOfRequestedItem;
        NumberOfExchangeItem = numberOfExchangeItem;
        Payment = payment;
    }
}

/// <summary>
/// Client -> server: this client's local copy of the issue finalized outside of a mirrored replay (e.g.
/// the accepting player just turned the quest in). A client's <c>SendAll</c> only reaches its one
/// connection - the server - which then replays the finalize on its own authoritative copy and broadcasts
/// <see cref="NetworkVillageIssueRemoved"/> to every peer.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly VillageIssueFinalizeReason Reason;

    public RequestVillageIssueRemoved(string ownerId, VillageIssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}

/// <summary>Server -> all clients: mirror the removal/teardown of this hero's Village Needs Tools issue.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly VillageIssueFinalizeReason Reason;

    public NetworkVillageIssueRemoved(string ownerId, VillageIssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}

/// <summary>
/// Client -> server: "hero X's issue giver just accepted the quest solution in my live conversation." The
/// requesting client already applied the transition locally (see <see cref="Patches.IssueAcceptancePatches"/>
/// for why it can't be blocked pending confirmation) - this lets the server arbitrate a same-issue
/// double-accept race and confirm/reject it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueAcceptQuest : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageIssueAcceptQuest(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients: confirms the quest-solution accept for this hero's issue; every other
/// peer mirrors it via <c>VillageNeedsToolsIssueInterface.MirrorQuestAccepted</c>. <see cref="OwnerControllerId"/>
/// is the accepting player's <c>ControllerId</c>, resolved server-side (either the host's own, for the
/// <c>ModInformation.IsServer</c> accept branch, or via <c>IPlayerManager.TryGetPlayer(NetPeer, out Player)</c>
/// from the authenticated requester for the arbitrated-race branch - NEVER trusted from a client-claimed
/// value, which is the load-bearing security point: a client cannot claim a different id to steal ownership).
/// Every peer, including the original accepter, records this via
/// <see cref="Interfaces.VillageNeedsToolsIssueOwnership.SetOwner"/> alongside the existing mirror call - see
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/>.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueQuestAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkVillageIssueQuestAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

/// <summary>Client -> server equivalent of <see cref="RequestVillageIssueAcceptQuest"/> for the
/// alternative-solution accept option.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct RequestVillageIssueAcceptAlternative : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public RequestVillageIssueAcceptAlternative(string ownerId)
    {
        OwnerId = ownerId;
    }
}

/// <summary>Server -> all clients equivalent of <see cref="NetworkVillageIssueQuestAccepted"/> for the
/// alternative-solution accept option. See that type for what <see cref="OwnerControllerId"/> is and how
/// it's resolved.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueAlternativeAccepted : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string OwnerControllerId;

    public NetworkVillageIssueAlternativeAccepted(string ownerId, string ownerControllerId)
    {
        OwnerId = ownerId;
        OwnerControllerId = ownerControllerId;
    }
}

/// <summary>
/// Server -> the one losing requester of a same-issue double-accept race: someone else's accept was
/// confirmed first, so roll this peer's own already-applied local acceptance back (see
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface.RejectAcceptance"/>). Deliberately a single message
/// shared by both accept paths - the rollback (<c>IssueBase.CompleteIssueWithCancel()</c>) is the same call
/// either way.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageIssueAcceptRejected : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;

    public NetworkVillageIssueAcceptRejected(string ownerId)
    {
        OwnerId = ownerId;
    }
}
