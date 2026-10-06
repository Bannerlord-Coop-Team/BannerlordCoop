using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Hitting client to peers over the mission mesh: a hit on a hull it does not simulate. Only the hull's current authority
/// applies it, through the vanilla damage method of <see cref="Kind"/>; every other peer drops it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkApplyShipDamage : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string SenderControllerId;
    [ProtoMember(3)] public readonly BattleShipDamageKind Kind;
    [ProtoMember(4)] public readonly float Damage;
    /// <summary>The sail damage after the attacker's sail fire model (Sails only).</summary>
    [ProtoMember(5)] public readonly float InflictedDamage;
    [ProtoMember(6)] public readonly float LocalX;
    [ProtoMember(7)] public readonly float LocalY;
    [ProtoMember(8)] public readonly float LocalZ;
    /// <summary>The attacking agent's registry id, or empty.</summary>
    [ProtoMember(9)] public readonly Guid AttackerAgentId;
    /// <summary>The ramming or colliding hull's ship id (Collision only), or empty.</summary>
    [ProtoMember(10)] public readonly Guid HitterShipId;
    [ProtoMember(11)] public readonly bool IsRamDamage;
    /// <summary>The hit sail's index in the hull's sails, or -1.</summary>
    [ProtoMember(12)] public readonly int SailIndex;
    /// <summary>The sender's battle host epoch; an AI hull's owner applies only hits stamped with its own epoch.</summary>
    [ProtoMember(13)] public readonly int HostEpoch;

    public NetworkApplyShipDamage(Guid shipId, string senderControllerId, BattleShipDamageKind kind, float damage,
        float inflictedDamage, Vec3 localPoint, Guid attackerAgentId, Guid hitterShipId, bool isRamDamage, int sailIndex,
        int hostEpoch)
    {
        ShipId = shipId;
        SenderControllerId = senderControllerId;
        Kind = kind;
        Damage = damage;
        InflictedDamage = inflictedDamage;
        LocalX = localPoint.x;
        LocalY = localPoint.y;
        LocalZ = localPoint.z;
        AttackerAgentId = attackerAgentId;
        HitterShipId = hitterShipId;
        IsRamDamage = isRamDamage;
        SailIndex = sailIndex;
        HostEpoch = hostEpoch;
    }

    public Vec3 LocalPoint => new Vec3(LocalX, LocalY, LocalZ);

    public bool IsValid => Enum.IsDefined(typeof(BattleShipDamageKind), Kind) && IsFinite(Damage) && Damage >= 0f
        && IsFinite(InflictedDamage) && IsFinite(LocalX) && IsFinite(LocalY) && IsFinite(LocalZ);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
