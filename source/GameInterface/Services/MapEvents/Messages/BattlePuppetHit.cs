using Common.Messaging;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event: a locally-controlled agent landed a damaging blow on a PUPPET (an agent owned
/// by another client) in a coop field battle. The blow is suppressed locally — a puppet must not take damage
/// on a node that doesn't own it, or the two clients' battles diverge — and the Missions battle controller
/// routes the damage to the puppet's owner, which applies it authoritatively. Published by
/// <see cref="Patches.BattleBlowInterceptPatch"/>.
/// </summary>
public record BattlePuppetHit : IEvent
{
    public Agent Victim { get; }
    public float Damage { get; }

    public BattlePuppetHit(Agent victim, float damage)
    {
        Victim = victim;
        Damage = damage;
    }
}
