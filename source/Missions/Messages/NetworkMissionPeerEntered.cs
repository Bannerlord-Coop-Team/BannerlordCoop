using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Server → existing instance members: a controller has entered the mission instance. This replaces the
/// direct <c>PeerConnected</c> trigger — on receiving it, a member sends its
/// <see cref="NetworkMissionJoinInfo"/> to <see cref="ControllerId"/> over the IBattleNetwork mesh.
/// <para>
/// The server fans this out (both directions) when it receives a client's <c>MissionEntered</c>, so the
/// join-info handshake no longer depends on observing a direct P2P connection. The notification itself
/// travels over the campaign/relay connection; the join info still flows over the mesh.
/// </para>
/// </summary>
[ProtoContract]
public readonly struct NetworkMissionPeerEntered : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public NetworkMissionPeerEntered(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
