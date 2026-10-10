using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>Returns a session-bound NAT token for one client mission-entry request.</summary>
[ProtoContract]
public readonly struct NetworkMissionIntroductionAuthorized : IEvent
{
    [ProtoMember(1)]
    public readonly string InstanceId;

    [ProtoMember(2)]
    public readonly Guid RequestId;

    [ProtoMember(3)]
    public readonly string Token;

    public NetworkMissionIntroductionAuthorized(string instanceId, Guid requestId, string token)
    {
        InstanceId = instanceId;
        RequestId = requestId;
        Token = token;
    }
}
