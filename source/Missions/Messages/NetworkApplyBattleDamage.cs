using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// Attacker's node → peers (over the mesh): a local troop hit a PUPPET, so route the WHOLE blow to that
/// puppet's owner. Only the owner (the node with authority) acts on it — re-applying the blow through
/// <c>Agent.RegisterBlow</c> so the engine resolves real damage, hit reaction, ragdoll and death (the death
/// then flows through <c>Agent.Die</c> → the death/casualty sync). Routing the real blow (instead of a bare
/// damage number + a synthetic kill) keeps combat faithful and removes the fixed-magnitude "fatal blow".
/// <para>
/// Agent indices are per-client, so the blow's attacker index can't be used as-is: <see cref="AttackerAgentId"/>
/// carries the attacker's network id and the owner re-maps it to its local agent's index. Missiles are not
/// synced (the projectile is simulated only on the shooter), but <c>Agent.RegisterBlow</c> resolves damage
/// from the blow and never dereferences the projectile index — so a routed missile blow applies cleanly with
/// no fix-up.
/// </para>
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkApplyBattleDamage : IEvent
{
    [ProtoMember(1)]
    public Guid VictimAgentId { get; }
    /// <summary>Network id of the attacker, or <see cref="Guid.Empty"/> if it couldn't be resolved.</summary>
    [ProtoMember(2)]
    public Guid AttackerAgentId { get; }
    [ProtoMember(3)]
    public Blow Blow { get; }
    [ProtoMember(4)]
    public AttackCollisionData CollisionData { get; }
    /// <summary>True when the blow targets <see cref="VictimAgentId"/>'s mount, not the rider itself. Fallback
    /// path only: a REGISTERED mount is routed by its own id (IsMount stays false); an unregistered horse is
    /// keyed off its rider's id and the owner resolves the rider's current MountAgent at apply time.</summary>
    [ProtoMember(5)]
    public bool IsMount { get; }

    public NetworkApplyBattleDamage(Guid victimAgentId, Guid attackerAgentId, Blow blow, AttackCollisionData collisionData, bool isMount = false)
    {
        VictimAgentId = victimAgentId;
        AttackerAgentId = attackerAgentId;
        Blow = blow;
        CollisionData = collisionData;
        IsMount = isMount;
    }
}
