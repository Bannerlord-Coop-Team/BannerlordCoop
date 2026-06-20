using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Server → remaining instance members: a controller has <em>gracefully</em> left the mission instance
/// (driven by its <c>MissionLeft</c>). The counterpart to <see cref="NetworkMissionPeerEntered"/> — on receiving
/// it a member despawns and deregisters the departed controller's party. This is the reliable,
/// server-mediated leave signal that complements the best-effort mesh <c>NetworkLeaveMission</c>.
/// </summary>
[ProtoContract]
public readonly struct MissionPeerLeft : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public MissionPeerLeft(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
