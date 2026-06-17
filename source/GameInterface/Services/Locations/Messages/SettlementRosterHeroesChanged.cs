using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Locations.Messages;

/// <summary>
/// Raised on the server when a governor or prisoner campaign event changes which heroes belong in a
/// settlement's location rosters. The population tracker reacts by re-running the vanilla placement
/// for the affected heroes and re-broadcasting the settlement roster, so clients already inside the
/// settlement update without waiting to leave and re-enter. Carries the live game objects because it
/// never crosses the network - it is published and handled within the server process.
/// </summary>
public readonly struct SettlementRosterHeroesChanged : IEvent
{
    public readonly Settlement Settlement;
    public readonly Hero[] Heroes;

    public SettlementRosterHeroesChanged(Settlement settlement, Hero[] heroes)
    {
        Settlement = settlement;
        Heroes = heroes;
    }
}
