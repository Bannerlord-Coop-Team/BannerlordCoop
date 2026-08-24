using Common.Messaging;
using ProtoBuf;
using System;

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

    /// <summary>
    /// Server-issued credential that binds this controller to its current mission membership.
    /// </summary>
    [ProtoMember(4)]
    public readonly Guid PeerCredential;

    public NetworkMissionPeerEntered(string controllerId, string instanceId)
        : this(controllerId, instanceId, 0, Guid.Empty)
    {
    }

    public NetworkMissionPeerEntered(string controllerId, string instanceId, ulong steamId)
        : this(controllerId, instanceId, steamId, Guid.Empty)
    {
    }

    public NetworkMissionPeerEntered(
        string controllerId,
        string instanceId,
        ulong steamId,
        Guid peerCredential)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        SteamId = steamId;
        PeerCredential = peerCredential;
    }
}
