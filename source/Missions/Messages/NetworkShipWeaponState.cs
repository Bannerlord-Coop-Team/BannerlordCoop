using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh when its hull's siege weapon changes aim, weapon state or ammo. Peers turn, load
/// and animate the same weapon on their copy, which never simulates it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipWeaponState : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    /// <summary>The weapon's content path below the hull, as <c>INavalShipEngine.TryGetWeapon</c> keys it.</summary>
    [ProtoMember(2)] public readonly string WeaponKey;
    [ProtoMember(3)] public readonly string OwnerControllerId;
    /// <summary>Increases with every change the owner sends; a new host's epoch restarts it for an AI hull.</summary>
    [ProtoMember(4)] public readonly long Revision;
    /// <summary>The sender's battle host epoch; an AI hull's state from a superseded host generation is dropped.</summary>
    [ProtoMember(5)] public readonly int HostEpoch;
    [ProtoMember(6)] public readonly BattleShipWeaponState State;

    public NetworkShipWeaponState(Guid shipId, string weaponKey, string ownerControllerId, long revision, int hostEpoch,
        BattleShipWeaponState state)
    {
        ShipId = shipId;
        WeaponKey = weaponKey;
        OwnerControllerId = ownerControllerId;
        Revision = revision;
        HostEpoch = hostEpoch;
        State = state;
    }
}
