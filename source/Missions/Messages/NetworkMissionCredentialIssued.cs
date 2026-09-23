using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Supplies the server-issued credential this client must present on mission peer connections.
/// </summary>
[ProtoContract]
public readonly struct NetworkMissionCredentialIssued : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    [ProtoMember(2)]
    public readonly Guid PeerCredential;

    public NetworkMissionCredentialIssued(string instanceId, Guid peerCredential)
    {
        InstanceId = instanceId;
        PeerCredential = peerCredential;
    }
}
