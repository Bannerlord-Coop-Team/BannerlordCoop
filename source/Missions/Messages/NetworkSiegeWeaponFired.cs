using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Mission host to peers: one ranged siege weapon (catapult/onager/ballista/trebuchet) just fired. The peer
/// plays the fire animation on the machine's skeleton and spawns a cosmetic projectile from the host's
/// resolved launch. Damage stays host-authoritative (routed blows + synced hit points); the peer's stone
/// does none because its shooter is a non-locally-controlled puppet, whose blows AgentDamagePatch drops.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeWeaponFired : IEvent
{
    [ProtoMember(1)]
    public int MachineId { get; }
    [ProtoMember(2)]
    public Guid ShooterAgentId { get; }
    [ProtoMember(3)]
    public Vec3 Position { get; }
    [ProtoMember(4)]
    public Vec3 Direction { get; }
    [ProtoMember(5)]
    public Mat3 Orientation { get; }
    [ProtoMember(6)]
    public float BaseSpeed { get; }
    [ProtoMember(7)]
    public float Speed { get; }
    /// <summary>The flying-missile item's StringId; resolved back via MBObjectManager on the peer. A raw
    /// ItemObject has no protobuf serializer, so it must not go on the wire.</summary>
    [ProtoMember(8)]
    public string MissileItemId { get; }

    public NetworkSiegeWeaponFired(int machineId, Guid shooterAgentId, Vec3 position, Vec3 direction, Mat3 orientation, float baseSpeed, float speed, string missileItemId)
    {
        MachineId = machineId;
        ShooterAgentId = shooterAgentId;
        Position = position;
        Direction = direction;
        Orientation = orientation;
        BaseSpeed = baseSpeed;
        Speed = speed;
        MissileItemId = missileItemId;
    }
}
