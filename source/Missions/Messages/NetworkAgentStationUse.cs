using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh: an owned agent started or stopped using a ship station (helm, oar, ship
/// weapon, rope or climbing point). Peers seat or release their puppet on the same point of their copy of the hull.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkAgentStationUse : IEvent
{
    [ProtoMember(1)] public readonly Guid AgentId;
    [ProtoMember(2)] public readonly Guid ShipId;
    /// <summary>The station point entity's content path below the hull entity.</summary>
    [ProtoMember(3)] public readonly string StationKey;
    /// <summary>The point's index among the usable points on that entity.</summary>
    [ProtoMember(4)] public readonly int PointIndex;
    [ProtoMember(5)] public readonly bool InUse;
    /// <summary>Per agent and sender, increasing with every use or release the owner sends.</summary>
    [ProtoMember(6)] public readonly long Revision;
    /// <summary>The owner that sent the use; a new owner after a migration numbers its revisions from 1 again.</summary>
    [ProtoMember(7)] public readonly string SenderControllerId;

    public NetworkAgentStationUse(Guid agentId, Guid shipId, string stationKey, int pointIndex, bool inUse, long revision,
        string senderControllerId)
    {
        AgentId = agentId;
        ShipId = shipId;
        StationKey = stationKey;
        PointIndex = pointIndex;
        InUse = inUse;
        Revision = revision;
        SenderControllerId = senderControllerId;
    }
}
