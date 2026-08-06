namespace GameInterface.Services.Issues.Generic;

// Not yet consumed by any migrated quest type; included ahead of its first real consumer.
public interface IResolvedIdSideEffectStrategy<TRequest>
{
    bool TryResolveLocally(out TRequest request);

    bool Revalidate(TRequest request);

    void ApplySideEffect(TRequest request);
}
