using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a locally-controlled agent landed a damaging blow on a PUPPET (an agent owned
/// by another client) in a coop field battle. The blow is suppressed locally — a puppet must not take damage
/// on a node that doesn't own it, or the two clients' battles diverge — and the Missions battle controller
/// routes the WHOLE blow to the puppet's owner, which re-applies it through <c>Agent.RegisterBlow</c> so the
/// engine resolves real damage, ragdoll and death (no synthetic kill). Published by
/// <see cref="Patches.BattleBlowInterceptPatch"/>.
/// </summary>
public record BattlePuppetHit : IEvent
{
    /// <summary>The puppet that was hit, or (for a mount hit) the puppet riding it — mounts are never
    /// registered/puppet-gated themselves, so ownership is always resolved through a rider.</summary>
    public Agent Victim { get; }
    /// <summary>The local attacker, resolved from <c>blow.OwnerId</c>; null if it couldn't be resolved.</summary>
    public Agent Attacker { get; }
    public Blow Blow { get; }
    public AttackCollisionData CollisionData { get; }
    /// <summary>True when the actual target of the blow is <see cref="Victim"/>'s mount, not the rider itself.</summary>
    public bool IsMount { get; }

    public BattlePuppetHit(Agent victim, Agent attacker, Blow blow, AttackCollisionData collisionData, bool isMount = false)
    {
        Victim = victim;
        Attacker = attacker;
        Blow = blow;
        CollisionData = collisionData;
        IsMount = isMount;
    }
}
