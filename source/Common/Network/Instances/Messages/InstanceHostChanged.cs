using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Local (broker-only) event published when host ownership of the current instance changes (e.g.
/// the previous host left). Consumed by <see cref="InstanceContext"/> so NPC simulation migrates.
/// </summary>
public record InstanceHostChanged : IEvent
{
    public string InstanceId { get; }
    public bool IsHost { get; }

    public InstanceHostChanged(string instanceId, bool isHost)
    {
        InstanceId = instanceId;
        IsHost = isHost;
    }
}
