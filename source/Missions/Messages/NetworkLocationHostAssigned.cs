using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Server → clients: the authoritative NPC host and ordered successor list for a settlement location
/// mission instance, sent in response to <see cref="NetworkRequestLocationHost"/>. The host owns native
/// NPC spawning and simulation; the successor list (next-in-line first) drives host migration if the
/// host leaves. The epoch (SR-016) is the server-issued hosting generation — it increments on every
/// host change, so a receiver holding a newer assignment ignores a stale (lower-epoch) broadcast
/// delivered out of order.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkLocationHostAssigned : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly string HostControllerId;
    [ProtoMember(3)]
    public readonly string[] SuccessorControllerIds = Array.Empty<string>();
    [ProtoMember(4)]
    public readonly int Epoch;

    public NetworkLocationHostAssigned(string instanceId, string hostControllerId, string[] successorControllerIds, int epoch)
    {
        InstanceId = instanceId;
        HostControllerId = hostControllerId;
        SuccessorControllerIds = successorControllerIds;
        Epoch = epoch;
    }
}
