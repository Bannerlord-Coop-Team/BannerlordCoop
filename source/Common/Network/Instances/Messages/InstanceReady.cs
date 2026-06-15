using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Broker event raised when the first remote peer spawns in the instance. Lets the campaign side drop
/// the joining client's "connecting to players" loading screen.
/// </summary>
public record InstanceReady : IEvent
{
    public string InstanceId { get; }

    public InstanceReady(string instanceId)
    {
        InstanceId = instanceId;
    }
}
