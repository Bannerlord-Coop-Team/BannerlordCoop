using Common.Messaging;
using Missions.Battles;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh when a hull's damage state or fieldable reserve changes. Peers set it on their copy,
/// so HP, fire and sinking follow the one client that applies damage to the hull, and the battle host's end check counts
/// the reserve.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCondition : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string OwnerControllerId;
    /// <summary>Increases with every change the owner sends; a new host's epoch restarts it for an AI hull.</summary>
    [ProtoMember(3)] public readonly long Revision;
    /// <summary>The sender's battle host epoch; AI-hull conditions from a superseded host generation are dropped.</summary>
    [ProtoMember(4)] public readonly int HostEpoch;
    [ProtoMember(5)] public readonly BattleShipCondition Condition;
    /// <summary>The troops queued on the hull that can still field, which only its owner tracks.</summary>
    [ProtoMember(6)] public readonly int SpawnableReserve;

    public NetworkShipCondition(Guid shipId, string ownerControllerId, long revision, int hostEpoch, BattleShipCondition condition,
        int spawnableReserve = 0)
    {
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        Revision = revision;
        HostEpoch = hostEpoch;
        Condition = condition;
        SpawnableReserve = spawnableReserve;
    }

    public AuthorityStamp Stamp => new AuthorityStamp(OwnerControllerId, HostEpoch, Revision);
}
