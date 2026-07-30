using Common.Messaging;
using System;

namespace GameInterface.Services.MapEvents.Messages.Start;

public readonly struct BattleJoinAccepted : IEvent
{
    public readonly string InstanceId;
    public readonly string ControllerId;
    public readonly Guid ReservationId;

    public BattleJoinAccepted(string instanceId, string controllerId, Guid reservationId)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
        ReservationId = reservationId;
    }
}

public readonly struct BattleJoinCancelled : IEvent
{
    public readonly string InstanceId;
    public readonly string ControllerId;
    public readonly Guid ReservationId;

    public BattleJoinCancelled(string instanceId, string controllerId, Guid reservationId = default)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
        ReservationId = reservationId;
    }
}
