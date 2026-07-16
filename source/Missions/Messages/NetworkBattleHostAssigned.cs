using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Server → clients: the authoritative battle host and ordered successor list for a map event, sent in
/// response to <see cref="NetworkRequestBattleHost"/>. The host owns troop spawning and the live mission
/// simulation; the successor list (next-in-line first) drives host migration if the host disconnects.
/// The epoch (BR-102) is the server-issued hosting generation — it increments on every host change, so a
/// receiver holding a newer assignment ignores a stale (lower-epoch) broadcast delivered out of order.
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
    [ProtoMember(4)]
    public readonly int Epoch;

    public NetworkBattleHostAssigned(string mapEventId, string hostControllerId, string[] successorControllerIds, int epoch)
    {
        MapEventId = mapEventId;
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
        Epoch = epoch;
    }
}
