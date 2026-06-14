using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Local (broker-only) event published by the P2P mission layer once the local player's instance is
/// populated — the first remote peer's agent has spawned in the live interior. Lets the campaign side
/// drop the "connecting to players" loading screen shown to a joining client while the interior loads
/// and the P2P link is established.
/// </summary>
public record InstanceReady : IEvent
{
    public string InstanceId { get; }

    public InstanceReady(string instanceId)
    {
        InstanceId = instanceId;
    }
}
