using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;

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
            logger.Information(
                "Finalizing loaded player map event {MapEventId} with {PartyCount} involved parties",
                mapEvent.StringId,
                mapEvent.InvolvedParties.Count());
            mapEvent.FinalizeEvent();
        }
    }
}
