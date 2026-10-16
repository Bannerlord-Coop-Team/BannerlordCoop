using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// A returning player to the battle host over the mission mesh: give back the adopted hull of my ship, which my own
/// launcher fielded again. Sent once its deployment is over; a repeat is answered the same way.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipReclaim : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string ReturningControllerId;
    [ProtoMember(3)] public readonly int HostEpoch;
    /// <summary>How many queued troops the returning player's hull can still take.</summary>
    [ProtoMember(4)] public readonly int FreeReserveSlots;

    public NetworkShipReclaim(Guid shipId, string returningControllerId, int hostEpoch, int freeReserveSlots)
    {
        ShipId = shipId;
        ReturningControllerId = returningControllerId;
        HostEpoch = hostEpoch;
        FreeReserveSlots = freeReserveSlots;
    }
}

/// <summary>
/// The battle host to every peer over the mission mesh: it released an adopted hull back to the player who fielded it,
/// with the hull's frame and condition at the release and the player's own queued troops the hull still held.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipHandedBack : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string HostControllerId;
    [ProtoMember(3)] public readonly string ReturningControllerId;
    [ProtoMember(4)] public readonly int HostEpoch;
    [ProtoMember(5)] public readonly float[] Frame;
    [ProtoMember(6)] public readonly BattleShipCondition Condition;
    [ProtoMember(7)] public readonly string ReservePartyId;
    [ProtoMember(8)] public readonly TroopReserveEntry[] Reserve;

    public NetworkShipHandedBack(Guid shipId, string hostControllerId, string returningControllerId, int hostEpoch, float[] frame,
        BattleShipCondition condition, string reservePartyId, TroopReserveEntry[] reserve)
    {
        ShipId = shipId;
        HostControllerId = hostControllerId;
        ReturningControllerId = returningControllerId;
        HostEpoch = hostEpoch;
        Frame = frame;
        Condition = condition;
        ReservePartyId = reservePartyId;
        Reserve = reserve ?? Array.Empty<TroopReserveEntry>();
    }

    public bool IsValid => ShipId != Guid.Empty && !string.IsNullOrEmpty(HostControllerId)
        && !string.IsNullOrEmpty(ReturningControllerId) && HostControllerId != ReturningControllerId
        && NetworkBattleShipSample.IsValidFrame(Frame) && Condition != null && Condition.IsValid;
}

/// <summary>The battle host to a returning player: why it keeps the hull that player asked back.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipReclaimRefused : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string HostControllerId;
    [ProtoMember(3)] public readonly string Reason;
    /// <summary>A refusal the returning player asks again after, such as a hull still being swapped or a newer host epoch.</summary>
    [ProtoMember(4)] public readonly bool IsTransient;

    public NetworkShipReclaimRefused(Guid shipId, string hostControllerId, string reason, bool isTransient)
    {
        ShipId = shipId;
        HostControllerId = hostControllerId;
        Reason = reason;
        IsTransient = isTransient;
    }
}
