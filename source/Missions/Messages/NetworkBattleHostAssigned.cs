using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Server → clients: the authoritative battle host and ordered successor list for a map event, sent in
/// response to <see cref="NetworkRequestBattleHost"/>. The host owns troop spawning and the live mission
/// simulation; the successor list (next-in-line first) drives host migration if the host disconnects.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkBattleHostAssigned : IEvent
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string HostControllerId;
    [ProtoMember(3)]
    public readonly string[] SuccessorControllerIds = Array.Empty<string>();

    public NetworkBattleHostAssigned(string mapEventId, string hostControllerId, string[] successorControllerIds)
    {
        MapEventId = mapEventId;
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
    }
}
