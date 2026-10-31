using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Library;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh: one projectile its hull's siege weapon launched, as vanilla resolved it. Peers
/// replay it as presentation only; the hit was decided on the owner and reaches the struck hull's owner as a routed hit.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipWeaponShot : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    /// <summary>The weapon's content path below the hull, as <c>INavalShipEngine.TryGetWeapon</c> keys it.</summary>
    [ProtoMember(2)] public readonly string WeaponKey;
    [ProtoMember(3)] public readonly string OwnerControllerId;
    /// <summary>The sender's battle host epoch; an AI hull's shot from a superseded host generation is dropped.</summary>
    [ProtoMember(4)] public readonly int HostEpoch;
    /// <summary>The registered agent that fired, or empty when it is not registered.</summary>
    [ProtoMember(5)] public readonly Guid ShooterAgentId;
    [ProtoMember(6)] public readonly Vec3 Position;
    [ProtoMember(7)] public readonly Vec3 Direction;
    [ProtoMember(8)] public readonly Mat3 Orientation;
    [ProtoMember(9)] public readonly float BaseSpeed;
    [ProtoMember(10)] public readonly float Speed;
    /// <summary>The flying missile item's StringId.</summary>
    [ProtoMember(11)] public readonly string MissileItemId;

    public NetworkShipWeaponShot(Guid shipId, string weaponKey, string ownerControllerId, int hostEpoch, Guid shooterAgentId,
        Vec3 position, Vec3 direction, Mat3 orientation, float baseSpeed, float speed, string missileItemId)
    {
        ShipId = shipId;
        WeaponKey = weaponKey;
        OwnerControllerId = ownerControllerId;
        HostEpoch = hostEpoch;
        ShooterAgentId = shooterAgentId;
        Position = position;
        Direction = direction;
        Orientation = orientation;
        BaseSpeed = baseSpeed;
        Speed = speed;
        MissileItemId = missileItemId;
    }

    public bool IsValid => Position.IsValid && Direction.IsValid && IsFinite(BaseSpeed) && IsFinite(Speed)
        && !string.IsNullOrEmpty(MissileItemId) && !string.IsNullOrEmpty(WeaponKey);

    // Whether a float off the wire is a real number.
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
