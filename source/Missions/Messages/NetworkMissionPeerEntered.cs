using Common.Messaging;
using Common.Network.Session;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Announces a mission member and optional provider identity over the campaign connection so both sides
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
    /// Provider-authenticated identity of <see cref="ControllerId"/>. Empty fields keep the
    /// server-relay fallback for direct-IP peers.
    /// </summary>
    [ProtoMember(3)]
    public readonly string Provider;

    [ProtoMember(4)]
    public readonly string UserId;

    public PlatformIdentity PeerIdentity => new PlatformIdentity(Provider, UserId);

    public NetworkMissionPeerEntered(string controllerId, string instanceId)
        : this(controllerId, instanceId, default)
    {
    }

    public NetworkMissionPeerEntered(
        string controllerId,
        string instanceId,
        PlatformIdentity peerIdentity)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        Provider = peerIdentity.Provider;
        UserId = peerIdentity.UserId;
    }
}
