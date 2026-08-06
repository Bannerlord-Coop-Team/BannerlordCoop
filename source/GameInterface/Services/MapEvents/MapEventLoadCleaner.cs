using Serilog;
using GameInterface.Services.MobileParties.Extensions;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GameInterface.Services.MapEvents;

public interface IMapEventLoadCleaner
{
    void FinalizePlayerMapEvents();
}

internal sealed class MapEventLoadCleaner : IMapEventLoadCleaner
{
    private readonly ILogger logger;

    public MapEventLoadCleaner(ILogger logger)
    {
        this.logger = logger;
    }

    public void FinalizePlayerMapEvents()
    {
        if (Campaign.Current?.MapEventManager == null)
            return;

        var loadedPlayerMapEvents = Campaign.Current.MapEventManager.MapEvents
            .Where(mapEvent => !mapEvent.IsFinalized && mapEvent.ContainsPlayerParty())
            .ToArray();

        foreach (var mapEvent in loadedPlayerMapEvents)
        {
            var involvedMobileParties = mapEvent.InvolvedParties
                .Where(party => party.IsMobile && party.MobileParty.IsActive)
                .Select(party => party.MobileParty)
                .ToArray();
            var playerLedArmies = involvedMobileParties
                .Select(mobileParty => mobileParty.Army)
                .Where(army => army != null && army.LeaderParty.IsPlayerParty())
                .Distinct()
                .ToArray();

            logger.Information(
                "Finalizing loaded player map event {MapEventId} with {PartyCount} involved parties",
                mapEvent.StringId,
                mapEvent.InvolvedParties.Count());
            mapEvent.FinalizeEvent();

            foreach (var army in playerLedArmies)
            {
                logger.Information(
                    "Dispersing loaded player-led army {ArmyName} released from map event {MapEventId}",
                    army.Name,
                    mapEvent.StringId);
                DisbandArmyAction.ApplyByUnknownReason(army);
            }

            foreach (var mobileParty in involvedMobileParties)
            {
                if (mobileParty.IsActive && !mobileParty.IsPlayerParty())
                    mobileParty.ResetNavigationToHold();
            }
        }
    }
}
