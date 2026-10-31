using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>The captured hull's authority to the captor over the mission mesh: it refused the capture.</summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkShipCaptureRejected : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string CaptorControllerId;
    [ProtoMember(3)] public readonly ShipCaptureRefusal Reason;

    public NetworkShipCaptureRejected(Guid shipId, string captorControllerId, ShipCaptureRefusal reason)
    {
        ShipId = shipId;
        CaptorControllerId = captorControllerId;
        Reason = reason;
    }
}

/// <summary>Why a capture did not happen: the authority's refusal reasons, plus the captor's own timeout.</summary>
public enum ShipCaptureRefusal
{
    /// <summary>The receiver does not simulate the hull (any more).</summary>
    NotAuthority,

    /// <summary>The captor already holds the hull from this same capture.</summary>
    AlreadyCaptured,

    CaptorNotInMission,

    /// <summary>The captor's own hull is not registered to it, has no formation left or is leaving.</summary>
    CaptorHullInvalid,

    /// <summary>The hull is on the captor's side and is not the captor's own orphan.</summary>
    NotEnemy,

    /// <summary>The hull is leaving, mid-swap, or a host promoted moments ago may still hear the old host's capture of it.</summary>
    Busy,

    CannotBeTakenOver,

    Sinking,

    /// <summary>Its crew still holds it: a unit of its formation is not swimming, or troops are queued on it.</summary>
    NotVacant,

    /// <summary>Never sent: nobody answered the captor's request in time.</summary>
    NoAnswer,

    /// <summary>Vanilla's swap can't run on the two hulls at the authority (a hull or formation it needs is missing).</summary>
    SwapUnavailable,
}
