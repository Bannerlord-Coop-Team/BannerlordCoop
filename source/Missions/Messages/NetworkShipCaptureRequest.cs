using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Captor to the captured hull's current authority over the mission mesh: its main agent held the hull's helm for vanilla's
/// capture time. The authority decides on its real crew and reserve and answers with <see cref="NetworkShipCaptured"/> to
/// everyone or <see cref="NetworkShipCaptureRejected"/> to the captor.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptureRequest : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    /// <summary>The captor's own hull, whose formation takes the prize.</summary>
    [ProtoMember(3)] public readonly Guid CaptorShipId;

    public NetworkShipCaptureRequest(Guid shipId, string captorControllerId, Guid captorShipId)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        CaptorShipId = captorShipId;
    }
}
