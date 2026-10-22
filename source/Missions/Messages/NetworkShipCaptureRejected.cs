using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>The captured hull's authority to peers over the mission mesh: it refused a capture; only the captor acts on it.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptureRejected : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    /// <summary>Why, as <c>BattleShipCaptureReplicator.Decide</c> names it.</summary>
    [ProtoMember(3)] public readonly string Reason;

    public NetworkShipCaptureRejected(Guid shipId, string captorControllerId, string reason)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        Reason = reason;
    }
}
