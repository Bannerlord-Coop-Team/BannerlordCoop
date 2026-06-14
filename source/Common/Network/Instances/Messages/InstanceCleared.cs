using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Local (broker-only) event published when the local player leaves the instance (returns to the
/// campaign). Clears <see cref="InstanceContext"/> so a later mission does not reuse a stale id.
/// </summary>
public record InstanceCleared : IEvent
{
}
