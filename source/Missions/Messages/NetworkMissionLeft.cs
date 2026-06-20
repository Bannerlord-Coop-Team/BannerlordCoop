using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Sent by a client to the server as it leaves a mission instance, so the server drops the client from the
/// instance's relay routing table. <see cref="InstanceId"/> has the same two forms as
/// <see cref="NetworkMissionEntered"/>.
/// </summary>
[ProtoContract]
public readonly struct NetworkMissionLeft : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public NetworkMissionLeft(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
