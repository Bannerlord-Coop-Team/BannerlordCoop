using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>Requests a NAT authorization on the campaign connection before punching.</summary>
[ProtoContract]
public readonly struct NetworkRequestMissionIntroduction : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    [ProtoMember(2)]
    public readonly Guid RequestId;

    public NetworkRequestMissionIntroduction(string instanceId, Guid requestId)
    {
        InstanceId = instanceId;
        RequestId = requestId;
    }
}
