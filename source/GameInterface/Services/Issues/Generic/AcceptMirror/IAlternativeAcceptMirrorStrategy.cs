using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Generic.AcceptMirror;

public interface IAlternativeAcceptMirrorStrategy<TPayload>
{
    void MirrorAlternativeAccepted(Hero owner, TPayload payload);

    void RejectAcceptance(Hero owner);
}

public interface IAlternativeSolutionPayloadFreeze<TPayload>
{
    void Freeze(Hero owner, TPayload payload);

    bool TryGetFrozen(Hero owner, out TPayload payload);
}

internal sealed class PendingRegistryPayloadFreeze<TPayload> : IAlternativeSolutionPayloadFreeze<TPayload>
{
    private readonly PendingRegistry<TPayload> _registry = new();

    public void Freeze(Hero owner, TPayload payload) => _registry.Set(owner, payload);
    public bool TryGetFrozen(Hero owner, out TPayload payload) => _registry.TryGet(owner, out payload);
    public void Clear(Hero owner) => _registry.Clear(owner);
    public void ClearAll() => _registry.ClearAll();

    public System.Collections.Generic.IReadOnlyCollection<System.Collections.Generic.KeyValuePair<Hero, TPayload>> Snapshot() => _registry.Snapshot();

    public void RestoreAll(System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Hero, TPayload>> entries) => _registry.RestoreAll(entries);
}

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
