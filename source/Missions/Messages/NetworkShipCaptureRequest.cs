using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Captor to peers over the mission mesh: its main agent held an enemy hull's helm for vanilla's capture time. Only the hull's
/// current authority acts on it, since only it holds the hull's real crew and reserve, and answers with
/// <see cref="NetworkShipCaptured"/> or <see cref="NetworkShipCaptureRejected"/>.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptureRequest : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    /// <summary>The captor's own hull, whose formation takes the prize.</summary>
    [ProtoMember(3)] public readonly Guid CaptorShipId;
    [ProtoMember(4)] public readonly int HostEpoch;

    public NetworkShipCaptureRequest(Guid shipId, string captorControllerId, Guid captorShipId, int hostEpoch)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        CaptorShipId = captorShipId;
        HostEpoch = hostEpoch;
    }
}
