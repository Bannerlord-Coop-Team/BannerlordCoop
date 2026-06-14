using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.Instances.Messages;

/// <summary>
/// Server -> client notification that instance host ownership has moved. Sent to the newly
/// elected host when the previous host left or disconnected, so NPC simulation migrates to it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkInstanceHostChanged : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly bool IsHost;

    public NetworkInstanceHostChanged(string instanceId, bool isHost)
    {
        InstanceId = instanceId;
        IsHost = isHost;
    }
}
