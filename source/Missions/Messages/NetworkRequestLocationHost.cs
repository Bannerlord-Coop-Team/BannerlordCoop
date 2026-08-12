using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → server: sent when the client's settlement location mission has FINISHED LOADING (it is
/// MISSION-READY, SR-010), asking the server to elect (or report) the authoritative NPC host for the
/// location instance. Carries the requester's controller id so the server records the mission-ready
/// order — the first to become ready is the host, the rest the successor line. The server replies with
/// <see cref="NetworkLocationHostAssigned"/>.
/// </summary>
[ProtoContract]
public readonly struct NetworkRequestLocationHost : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly string ControllerId;

    public NetworkRequestLocationHost(string instanceId, string controllerId)
    {
        InstanceId = instanceId;
        ControllerId = controllerId;
    }
}
