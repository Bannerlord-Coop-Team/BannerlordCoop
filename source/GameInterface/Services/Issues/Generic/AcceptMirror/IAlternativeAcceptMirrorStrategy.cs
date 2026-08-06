using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

/// <summary>
/// Alternative-solution race-arbitrated accept mirror. Deliberately separate from
/// <see cref="IRaceArbitratedAcceptMirrorStrategy{TFields}"/> - no Quest object exists to replay/force-write
/// onto for this path (<c>StartIssueWithAlternativeSolution</c> never calls <c>GenerateIssueQuest</c>), so there
/// is no <c>ReplayQuestAccepted</c> step and no "ensure quest exists" precondition. The target is the parent
/// <see cref="TaleWorlds.CampaignSystem.Issues.IssueBase"/>'s own state.
/// </summary>
public interface IAlternativeAcceptMirrorStrategy<TPayload>
{
    /// <summary>Force-writes the generic Issue-state transition common to EVERY real instance of this shape
    /// (<c>_issueState -&gt; SolvingWithAlternativeSolution</c>, <c>IsTriedToSolveBefore -&gt; true</c>,
    /// <c>IssueDueTime -&gt; CampaignTime.Never</c>), then applies <paramref name="payload"/> via whatever
    /// type-specific mechanism this instance needs (a no-op for B1's <see cref="Unit"/> payload;
    /// <see cref="IAlternativeSolutionPayloadFreeze{TPayload}.Freeze"/> for B2). A no-op if the issue is no
    /// longer <c>IsOngoingWithoutQuest</c>.</summary>
    void MirrorAlternativeAccepted(Hero owner, TPayload payload);

    /// <summary>Same guard requirement as Shape A's <c>RejectAcceptance</c> (check whether an owner is already
    /// recorded before rolling back) - the identical rollback-vs-already-mirrored-winner race applies to this
    /// shape too.</summary>
    void RejectAcceptance(Hero owner);
}

/// <summary>
/// Payload-freeze escape hatch: since this shape has no Quest field to force-write, a payload that must survive
/// to a later (days-later, completion-time) read needs a captured freeze target instead of a normal reflected
/// field. Structurally a <see cref="PendingRegistry{TValue}"/> with the same <c>Set</c>/<c>TryGet</c> shape,
/// consulted from property getters rather than field reads.
/// </summary>
public interface IAlternativeSolutionPayloadFreeze<TPayload>
{
    /// <summary>= <see cref="PendingRegistry{TValue}.Set"/>, named for call-site clarity.</summary>
    void Freeze(Hero owner, TPayload payload);

    /// <summary>= <see cref="PendingRegistry{TValue}.TryGet"/>.</summary>
    bool TryGetFrozen(Hero owner, out TPayload payload);
}

/// <summary>
/// <see cref="PendingRegistry{TValue}"/>-backed default implementation of
/// <see cref="IAlternativeSolutionPayloadFreeze{TPayload}"/> - a consumer can either implement the interface
/// itself or delegate to an instance of this class. Not used when the payload is <see cref="Unit"/> (nothing to
/// freeze).
/// </summary>
internal sealed class PendingRegistryPayloadFreeze<TPayload> : IAlternativeSolutionPayloadFreeze<TPayload>
{
    private readonly PendingRegistry<TPayload> _registry = new();

    public void Freeze(Hero owner, TPayload payload) => _registry.Set(owner, payload);
    public bool TryGetFrozen(Hero owner, out TPayload payload) => _registry.TryGet(owner, out payload);
    public void Clear(Hero owner) => _registry.Clear(owner);
    public void ClearAll() => _registry.ClearAll();

    /// <summary>Feeds a per-behavior <c>SyncData</c> persistence postfix - see
    /// <see cref="PendingRegistry{TValue}.Snapshot"/>.</summary>
    public System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<Hero, TPayload>> Snapshot() => _registry.Snapshot();

    /// <summary>Load-time repopulate - see <see cref="PendingRegistry{TValue}.RestoreAll"/>.</summary>
    public void RestoreAll(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Hero, TPayload>> entries) => _registry.RestoreAll(entries);
}

/// <summary>
/// Thin generic driver over <see cref="IAlternativeAcceptMirrorStrategy{TPayload}"/> - the alternative-solution
/// equivalent of <see cref="RaceArbitratedAcceptMirrorHandler{TFields}"/>.
/// </summary>
public sealed class AlternativeAcceptMirrorHandler<TPayload>
{
    private readonly IAlternativeAcceptMirrorStrategy<TPayload> _strategy;

    public AlternativeAcceptMirrorHandler(IAlternativeAcceptMirrorStrategy<TPayload> strategy)
    {
        _strategy = strategy;
    }

    public void Mirror(Hero owner, TPayload payload) => _strategy.MirrorAlternativeAccepted(owner, payload);

    public void Reject(Hero owner) => _strategy.RejectAcceptance(owner);
}
