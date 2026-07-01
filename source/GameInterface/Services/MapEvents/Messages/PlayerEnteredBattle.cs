using Common.Messaging;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Messages;

/// <summary>
/// Local (broker-only) event raised when the local player opens a field-battle mission for a
/// <see cref="MapEvent"/> — the battle counterpart to
/// <see cref="Locations.Messages.PlayerEnteredLocation"/>. The battle P2P controller resolves the
/// map event's object-manager id and uses it as the mission instance id, then connects to that
/// instance over the mission mesh network so every player fighting the same battle joins it.
/// </summary>
public record PlayerEnteredBattle : IEvent
{
    public MapEvent MapEvent { get; }

    public PlayerEnteredBattle(MapEvent mapEvent)
    {
        MapEvent = mapEvent;
    }
}
