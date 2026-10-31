using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// The captured hull's authority to peers over the mission mesh, after it applied the capture itself: every client replays
/// vanilla's swap, the prize follows <see cref="CaptorControllerId"/> from then on and the captor's own hull is left without a
/// formation. Each client applies captures of a hull in the order of its capture sequences, so senders need not be ordered.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptured : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    [ProtoMember(3)] public readonly Guid CaptorShipId;
    /// <summary>The authority that decided the capture and sent this.</summary>
    [ProtoMember(4)] public readonly string PreviousAuthority;
    /// <summary>The sender's battle host epoch when it decided; an AI hull's authority is that epoch's host.</summary>
    [ProtoMember(5)] public readonly int HostEpoch;
    /// <summary>How many captures the prize took part in before this one, on the sender.</summary>
    [ProtoMember(6)] public readonly int PrizeSequence;
    /// <summary>How many captures the captor's hull took part in before this one, on the sender.</summary>
    [ProtoMember(7)] public readonly int CaptorSequence;

    public NetworkShipCaptured(Guid shipId, string captorControllerId, Guid captorShipId, string previousAuthority, int hostEpoch,
        int prizeSequence, int captorSequence)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        CaptorShipId = captorShipId;
        PreviousAuthority = previousAuthority;
        HostEpoch = hostEpoch;
        PrizeSequence = prizeSequence;
        CaptorSequence = captorSequence;
    }
}
