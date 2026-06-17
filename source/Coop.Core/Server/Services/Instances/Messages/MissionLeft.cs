using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Messages;

/// <summary>
/// Sent by a client to the server as it leaves a mission instance, so the server drops the client from the
/// instance's relay routing table. <see cref="InstanceId"/> has the same two forms as
/// <see cref="MissionEntered"/>.
/// </summary>
[ProtoContract]
public readonly struct MissionLeft : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public MissionLeft(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
