using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Machine simulator to peers: one ranged siege weapon (catapult/onager/ballista/trebuchet) just fired. The peer
/// plays the fire animation on the machine's skeleton and spawns a cosmetic projectile from the simulator's
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
    /// <summary>Controller that simulated this machine when the projectile was launched.</summary>
    [ProtoMember(9)]
    public string SenderControllerId { get; }
    /// <summary>Host epoch for the machine authority that launched this projectile.</summary>
    [ProtoMember(10)]
    public int HostEpoch { get; }
    /// <summary>Per-machine authority revision that launched this projectile.</summary>
    [ProtoMember(11)]
    public int AuthorityRevision { get; }

    public NetworkSiegeWeaponFired(
        int machineId,
        Guid shooterAgentId,
        Vec3 position,
        Vec3 direction,
        Mat3 orientation,
        float baseSpeed,
        float speed,
        string missileItemId,
        string senderControllerId = null,
        int hostEpoch = 0,
        int authorityRevision = 0)
    {
        MachineId = machineId;
        ShooterAgentId = shooterAgentId;
        Position = position;
        Direction = direction;
        Orientation = orientation;
        BaseSpeed = baseSpeed;
        Speed = speed;
        MissileItemId = missileItemId;
        SenderControllerId = senderControllerId;
        HostEpoch = hostEpoch;
        AuthorityRevision = authorityRevision;
    }
}
