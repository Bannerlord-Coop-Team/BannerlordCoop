using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Sent by a client to the server as it enters a mission instance, so the server can map the client's
/// controller to the connection it arrived on for the relay fallback.
/// <para>
/// <see cref="InstanceId"/> is the client-derived instance id and has two forms, both opaque to the
/// server: a location (<c>Settlement.StringId</c> + <c>Location.StringId</c>) or a map event
/// (<c>MapEvent.StringId</c>). It MUST match the id the same client uses for its NAT-punch request, or the
/// relay context and the punch endpoints end up in different instances.
/// </para>
/// </summary>
[ProtoContract]
public readonly struct NetworkMissionEntered : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public NetworkMissionEntered(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
