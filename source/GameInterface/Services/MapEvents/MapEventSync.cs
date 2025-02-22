using GameInterface.AutoSync;

namespace GameInterface.Services.MapEvents;

internal class MapEventSync : IAutoSync
{
    public MapEventSync(IAutoSyncBuilder autoSyncBuilder)
    {
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedInfluence)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GainedRenown)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.GoldLost)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.MoraleChange)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.Party)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventParty), nameof(MapEventParty.PlunderedGold)));
    }
}