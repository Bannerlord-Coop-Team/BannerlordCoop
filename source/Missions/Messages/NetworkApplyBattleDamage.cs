using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Messages;

/// <summary>
/// Attacker's node to peers over the mesh: a local troop hit a puppet, so route the resolved blow to
/// that puppet's owner. Only the owner replays it through <c>Agent.RegisterBlow</c>.
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
    public BattleDamageData DamageData { get; }
    /// <summary>True when the blow targets <see cref="VictimAgentId"/>'s mount, not the rider itself.</summary>
    [ProtoMember(5)]
    public bool IsMount { get; }
    /// <summary>Identity of the exact missile launch that produced this blow.</summary>
    [ProtoMember(6)]
    public long MissileShotSequence { get; }
    [ProtoMember(7)]
    public bool IsMissile { get; }
    /// <summary>The original missile weapon used by vanilla to select the combat skill reward.</summary>
    [ProtoMember(8)]
    public WeaponComponentData AttackerWeapon { get; }

    [ProtoIgnore]
    public Blow Blow { get; private set; }
    [ProtoIgnore]
    public AttackCollisionData CollisionData { get; private set; }

    public NetworkApplyBattleDamage(
        Guid victimAgentId,
        Guid attackerAgentId,
        BattleDamageData damageData,
        bool isMissile,
        bool isMount = false,
        long missileShotSequence = 0,
        WeaponComponentData attackerWeapon = null)
    {
        VictimAgentId = victimAgentId;
        AttackerAgentId = attackerAgentId;
        DamageData = damageData;
        IsMount = isMount;
        MissileShotSequence = missileShotSequence;
        IsMissile = isMissile;
        AttackerWeapon = attackerWeapon;
    }

    internal void AttachDecodedData(Blow blow, AttackCollisionData collisionData)
    {
        Blow = blow;
        CollisionData = collisionData;
    }
}
