using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Local (broker-only) event raised when the local player opens an interior mission for a
/// <see cref="Location"/> (tavern, town center, etc.). A handler resolves the object-manager ids
/// and turns it into a <see cref="Common.Network.Instances.Messages.EnterLocationRequested"/> so
/// the server can assign a P2P instance.
/// </summary>
public record PlayerEnteredLocation : IEvent
{
    public Settlement Settlement { get; }
    public Location Location { get; }

    public PlayerEnteredLocation(Settlement settlement, Location location)
    {
        Settlement = settlement;
        Location = location;
    }
}
