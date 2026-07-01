using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Client → server: sent as the client enters a battle mission instance, asking the server to elect (or
/// report) the authoritative battle host for the map event. Carries the requester's controller id so the
/// server can record join order — the first to enter becomes the host, the rest the successor line. The
/// server replies with <see cref="NetworkBattleHostAssigned"/>.
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
