using System;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// The single "am I authoritative over this agent" seam for a coop battle. Collapses the four historical
/// encodings — the registry's own <c>IsLocallyControlled</c>, the container-resolving
/// <c>AgentExtensions.IsLocallyControlled</c> hot path, raw <c>CoopAgentInfo.CurrentAuthority</c> compares, and
/// the engine's <see cref="AgentControllerType.None"/> puppet signal — into one implementation owned by the
/// Missions battle stack (over the per-mission agent registry + battle session).
/// <para>
/// It is exposed to the DI-less GameInterface Harmony patches through a single static bridge
/// (<see cref="BattleSpawnGate.AgentAuthority"/>), installed by the battle controller on entry and cleared on
/// dispose — the replacement for the old <c>MountAuthorityProbe</c> delegate. Read-path only: it does not move
/// authority (adoption/transfer stays in <c>BattleAuthorityMigrator</c>).
/// </para>
/// </summary>
public interface IAgentAuthority
{
    /// <summary>
    /// True when this client is authoritative over <paramref name="agent"/> — it is registered and its current
    /// authority is our controller. Unregistered agents are never "mine".
    /// </summary>
    bool IsMine(Agent agent);

    /// <summary>
    /// True when this client is authoritative over the agent with <paramref name="agentId"/> — it is registered
    /// and its current authority is our controller. Unknown ids are never "mine".
    /// </summary>
    bool IsMine(Guid agentId);

    /// <summary>
    /// True when <paramref name="agent"/> is a puppet this client must NOT simulate — a blow on it is suppressed
    /// locally and routed to its owner. Full tri-state decision tree, not a registry-only lookup:
    /// <list type="bullet">
    /// <item>registered agent → puppet iff another controller holds its authority;</item>
    /// <item>unregistered human → the engine controller decides (<see cref="AgentControllerType.None"/> = puppet),
    /// keeping blows suppressed on fading / just-deregistered withdrawing troops during the fade-out window;</item>
    /// <item>unregistered mount → keyed off its rider's ownership (masterless or own-ridden horse = take the blow
    /// locally; a puppet-ridden horse = route it), preserving the old mount fallback semantics.</item>
    /// </list>
    /// </summary>
    bool IsPuppet(Agent agent);

    /// <summary>
    /// The controller id currently authoritative over <paramref name="agent"/>, or <c>null</c> when it is not
    /// registered. LOCAL-VIEW ONLY: on non-promoted clients this is intentionally stale after a migration
    /// (damage routing is broadcast-and-owner-filtered, so only the local "is it mine" answer matters) — callers
    /// must not treat it as a cross-client source of truth.
    /// </summary>
    string Owner(Agent agent);
}
