using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → server: sent when the client's battle mission has FINISHED LOADING (it is MISSION-READY,
/// BR-010), asking the server to elect (or report) the authoritative battle host for the map event. Carries
/// the requester's controller id so the server can record the mission-ready order (BR-013) — the first to
/// become ready is the host, the rest the successor line. The server replies with
/// <see cref="NetworkBattleHostAssigned"/> plus the requester's owned reserves (the NPC grant for the host).
/// </summary>
[ProtoContract]
public readonly struct NetworkRequestBattleHost : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string ControllerId;

    public NetworkRequestBattleHost(string mapEventId, string controllerId)
    {
        MapEventId = mapEventId;
        ControllerId = controllerId;
    }
}
