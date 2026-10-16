using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>Why an owner's hull left a naval battle.</summary>
public enum ShipRemovalReason
{
    /// <summary>The owner retreated from the battle, or the hull sailed off the map under a Retreat order.</summary>
    Retreat,

    /// <summary>The owner left the battle any other way, or the hull left the mission without a retreat.</summary>
    Leave,
}

/// <summary>
/// Owner to peers over the mission mesh as its hull leaves the mission (vanilla's BeforeShipRemovedEvent). Peers remove
/// their copy the same way: seats released, other owners' agents on its deck moved to their own hull, then the hull goes.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipRemoved : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string OwnerControllerId;
    [ProtoMember(3)] public readonly ShipRemovalReason Reason;

    public NetworkShipRemoved(Guid shipId, string ownerControllerId, ShipRemovalReason reason)
    {
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        Reason = reason;
    }
}
