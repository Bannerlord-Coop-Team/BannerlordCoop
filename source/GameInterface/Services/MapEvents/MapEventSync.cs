using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

internal class MapEventSync : IDynamicSync
{
    public MapEventSync(DynamicSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.RetreatingSide)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.BattleState)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Component)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsInvulnerable)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsPlayerSimulation)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsVisible)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.MapEventSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Position)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.State)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker)), debug: true);

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.DiplomaticallyFinished)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.StrengthOfSide)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleResultExplainers)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventResultsCalculated)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._eventTerrainType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._isFinishCalled)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._isVisible)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._keepSiegeEvent)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventStartTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventResultsApplied)));
        // Collection of sides for the MapEvent
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), "_sides"));
    }
}