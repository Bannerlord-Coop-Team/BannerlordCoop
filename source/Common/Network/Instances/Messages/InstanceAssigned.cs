using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Local (broker-only) event published by the client networking layer when the server assigns this
/// client a P2P instance. Consumed by <see cref="InstanceContext"/> and the mission/P2P layer to
/// drive NAT punching with the server-issued instance id.
/// </summary>
public record InstanceAssigned : IEvent
{
    public string InstanceId { get; }
    public bool IsHost { get; }
    public string SettlementId { get; }
    public string LocationId { get; }

    public InstanceAssigned(string instanceId, bool isHost, string settlementId, string locationId)
    {
        InstanceId = instanceId;
        IsHost = isHost;
        SettlementId = settlementId;
        LocationId = locationId;
    }
}
