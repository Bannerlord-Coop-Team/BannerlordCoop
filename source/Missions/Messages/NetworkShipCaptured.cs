using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// The captured hull's authority to peers over the mission mesh, after it applied the capture itself: every client replays
/// vanilla's swap, the prize follows <see cref="CaptorControllerId"/> from then on and the captor's own hull is left without a
/// formation. Applied even after its sender left, since mesh routes aren't ordered against the server's departure events.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptured : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    [ProtoMember(3)] public readonly Guid CaptorShipId;
    /// <summary>The authority that decided the capture and sent this.</summary>
    [ProtoMember(4)] public readonly string PreviousAuthority;
    [ProtoMember(5)] public readonly int HostEpoch;

    public NetworkShipCaptured(Guid shipId, string captorControllerId, Guid captorShipId, string previousAuthority, int hostEpoch)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        CaptorShipId = captorShipId;
        PreviousAuthority = previousAuthority;
        HostEpoch = hostEpoch;
    }
}
