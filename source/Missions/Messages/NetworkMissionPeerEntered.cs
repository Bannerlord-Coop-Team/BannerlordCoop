using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Announces a mission member and optional Steam identity over the campaign connection so both sides
/// can establish their mesh link and exchange <see cref="NetworkMissionJoinInfo"/>.
/// </summary>
[ProtoContract]
public readonly struct NetworkMissionPeerEntered : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    /// <summary>
    /// Steam identity of <see cref="ControllerId"/>, when the server can resolve one. Zero keeps the
    /// existing server-relay fallback for direct-IP peers and any Steam identity that is unavailable.
    /// </summary>
    [ProtoMember(3)]
    public readonly ulong SteamId;

    public NetworkMissionPeerEntered(string controllerId, string instanceId)
        : this(controllerId, instanceId, 0)
    {
    }

    public NetworkMissionPeerEntered(string controllerId, string instanceId, ulong steamId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        SteamId = steamId;
    }
}
