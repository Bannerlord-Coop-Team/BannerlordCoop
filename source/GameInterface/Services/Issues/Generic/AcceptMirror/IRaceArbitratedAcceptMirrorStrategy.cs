using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

/// <summary>
/// Quest-solution race-arbitrated accept. Common precondition: a real
/// <see cref="TaleWorlds.CampaignSystem.Issues.QuestBase"/> object gets built via
/// <c>GenerateIssueQuest</c>/<c>StartIssueQuest</c>, so a server-side <see cref="ReplayQuestAccepted"/> is
/// possible and authoritative, and <typeparamref name="TFields"/> gets force-written onto that Quest object by
/// reflection (see <see cref="IAcceptMirrorStrategy{TQuest,TFields}"/>).
///
/// Explicitly scoped to quest-solution accept only - the alternative-solution path is the separate,
/// deliberately not-consolidated <see cref="IAlternativeAcceptMirrorStrategy{TPayload}"/>.
/// </summary>
public interface IRaceArbitratedAcceptMirrorStrategy<TFields>
{
    /// <summary>[Server-only, while arbitrating a client's accept request] Bare, uncorrected replay of
    /// <c>IssueManager.StartIssueQuest</c> for an owner whose issue is still <c>IsOngoingWithoutQuest</c>. The
    /// server has no authoritative fields to force-write yet - this replay IS what produces them (the server's
    /// own roll is authoritative by definition) - so the server calls this bare method first, then reads back
    /// what it produced via <see cref="TryCaptureQuestFields"/> before broadcasting. A no-op if the issue is no
    /// longer <c>IsOngoingWithoutQuest</c>.</summary>
    void ReplayQuestAccepted(Hero owner);

    /// <summary>Reads back what a GENUINE accept (either this machine's own live conversation, or the server's
    /// own <see cref="ReplayQuestAccepted"/>) just authoritatively baked, for broadcasting. Returns false if the
    /// owner has no quest of this type yet.</summary>
    bool TryCaptureQuestFields(Hero owner, out TFields fields);

    /// <summary>The central mechanism: ensures <paramref name="owner"/> has a real quest object (replaying if it
    /// doesn't yet), then force-writes <paramref name="fields"/> onto it. Idempotent: safe to call again with the
    /// same values (a resend, or the server's own broadcast echoing back to itself). A no-op entirely if
    /// <paramref name="owner"/>'s issue isn't this strategy's own issue type.</summary>
    void MirrorQuestAccepted(Hero owner, TFields fields);

    /// <summary>Rolls a losing peer's own already-applied (optimistic) local acceptance back after the server
    /// tells it another peer won the same-issue accept race. MUST check whether an owner is already recorded
    /// before rolling back (the winning accept's own broadcast may have already reached this peer and correctly
    /// mirrored the real winner's quest by the time this rejection arrives) - same fix shape as every real
    /// conforming type's own <c>RejectAcceptance</c>.</summary>
    void RejectAcceptance(Hero owner);
}

/// <summary>
/// Thin generic driver over <see cref="IRaceArbitratedAcceptMirrorStrategy{TFields}"/>. Message-type wiring
/// (the concrete networked request/broadcast/reject messages) remains hand-written per instance - this only
/// factors out the arbitration logic a per-type Handler class would otherwise duplicate.
/// </summary>
public sealed class RaceArbitratedAcceptMirrorHandler<TFields>
{
    private readonly IRaceArbitratedAcceptMirrorStrategy<TFields> _strategy;

    public RaceArbitratedAcceptMirrorHandler(IRaceArbitratedAcceptMirrorStrategy<TFields> strategy)
    {
        _strategy = strategy;
    }

    /// <summary>[Server-only] First valid request wins the race: <paramref name="canAccept"/> re-checks the
    /// issue is still genuinely acceptable (the same "is this the right issue type AND still
    /// IsOngoingWithoutQuest" check every real handler already does), replays the accept, and captures what the
    /// replay produced. Returns false (nothing to broadcast, caller should reject the requester) if the issue was
    /// no longer acceptable or the replay didn't produce a readable quest.</summary>
    public bool TryArbitrate(Hero owner, System.Func<Hero, bool> canAccept, out TFields fields)
    {
        fields = default;
        if (owner == null || canAccept == null || !canAccept(owner)) return false;

        _strategy.ReplayQuestAccepted(owner);
        return _strategy.TryCaptureQuestFields(owner, out fields);
    }

    /// <summary>[Whichever machine's accept just genuinely ran locally - e.g. the real accept-capture postfix
    /// that fires immediately after a GENUINE, non-replayed <c>IssueManager.StartIssueQuest</c>] Bare read-back
    /// of what that accept just baked, with no replay - unlike <see cref="TryArbitrate"/>, which is the
    /// server-only "replay then capture" arbitration entry point.</summary>
    public bool TryCaptureQuestFields(Hero owner, out TFields fields) => _strategy.TryCaptureQuestFields(owner, out fields);

    /// <summary>[Every peer, including the genuine accepter's own machine] Force-mirrors the authoritative
    /// fields to converge every peer's own quest object.</summary>
    public void Mirror(Hero owner, TFields fields) => _strategy.MirrorQuestAccepted(owner, fields);

    /// <summary>[The one losing peer of a same-issue double-accept race] Rolls this peer's own optimistic local
    /// accept back.</summary>
    public void Reject(Hero owner) => _strategy.RejectAcceptance(owner);
}
