using Common.Messaging;

namespace Common.Network.Instances.Messages;

/// <summary>
/// Local (broker-only) event published when the local player's party enters an interior location.
/// The client networking layer turns this into a server request for a P2P instance assignment.
/// Carries object-manager string ids so the server can key the instance by settlement + location.
/// </summary>
public record EnterLocationRequested : IEvent
{
    public string SettlementId { get; }
    public string LocationId { get; }

    public EnterLocationRequested(string settlementId, string locationId)
    {
        SettlementId = settlementId;
        LocationId = locationId;
    }
}
