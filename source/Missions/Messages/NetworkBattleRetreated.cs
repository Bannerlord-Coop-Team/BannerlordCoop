using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Sent by a battle client when it deliberately withdraws from an unresolved battle. Unlike
/// <see cref="NetworkMissionLeft"/>, normal teardown after a resolved battle does not send this message.
/// </summary>
[ProtoContract]
public readonly struct NetworkBattleRetreated : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    public NetworkBattleRetreated(string instanceId)
    {
        InstanceId = instanceId;
    }
}