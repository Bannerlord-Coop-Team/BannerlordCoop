namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Ground truth: <c>LordWantsRivalCapturedBattleWonPatches.OnPlayerBattleEventEndedPrefix</c> evaluates
/// <c>mapEvent.PlayerSide</c>/<c>HasWinner</c>/<c>WinningSide</c> locally, publishes resolved ids including this
/// machine's own <c>Hero.MainHero</c>; the handler re-validates against several independent guard clauses, then
/// calls a shared, idempotency-guarded consequence chokepoint. Critically, this side effect does NOT
/// complete/finalize anything (that's always a separate, later trigger) - the same chokepoint shape recurs in
/// <c>BattleMissionStartHandler.ApplyClientAttackHostileConsequences</c>. Message-type wiring remains
/// hand-written per instance - this interface names and documents the contract, it does not eliminate per-type
/// plumbing. Not consumed by any migrated quest type so far (none has a locally-resolved-id side effect of this
/// shape) - included as unused infrastructure for one that eventually does.
/// </summary>
public interface IResolvedIdSideEffectStrategy<TRequest>
{
    /// <summary>Runs ONLY on the machine where the local-only condition genuinely fired - NEVER server-side,
    /// NEVER re-derivable from ambient MainParty/MainHero state (a dedicated host has no
    /// <c>MobileParty.MainParty</c> at all).</summary>
    bool TryResolveLocally(out TRequest request);

    /// <summary>Server-side: independent re-validation, trusting only the ids supplied, never the client's own
    /// conclusion.</summary>
    bool Revalidate(TRequest request);

    /// <summary>The mutation itself - MUST be idempotent under a caller-supplied guard. Void return by design:
    /// this shape is defined by NOT completing/finalizing anything.</summary>
    void ApplySideEffect(TRequest request);
}
