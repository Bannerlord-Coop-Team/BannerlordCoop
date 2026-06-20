using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Missions.Messages;

/// <summary>
/// Server → remaining instance members: a controller dropped <em>ungracefully</em> (the server observed its
/// <c>OnPeerDisconnected</c>). Unlike the mesh path, the server reliably detects the drop even when the
/// P2P link silently dies. On receiving it a member releases the departed controller's party — in a
/// location that means despawning it; in a battle the host instead assumes control of it.
/// </summary>
[ProtoContract]
public readonly struct MissionPeerDisconnected : IEvent
{
    [ProtoMember(1)]
    public readonly string ControllerId;

    [ProtoMember(2)]
    public readonly string InstanceId;

    public MissionPeerDisconnected(string controllerId, string instanceId)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
    }
}
