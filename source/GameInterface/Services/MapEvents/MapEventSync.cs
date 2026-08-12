using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

internal class MapEventSync : IAutoSync
{
    public MapEventSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.RetreatingSide)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.PursuitRoundNumber)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.BattleState)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Component)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsInvulnerable)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsPlayerSimulation)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.MapEventSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Position)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.State)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker)), debug: true);

        // DedicatedServer.Core supplies a private headless IMapEventVisual. It is authoritative server state,
        // but it has no corresponding client object and is intentionally absent from the ObjectManager.
        // Preserve the wire contract for graphical hosts while skipping only that unregistered reference.
        autoSyncBuilder.AddField(
            AccessTools.Field(typeof(MapEvent), nameof(MapEvent.MapEventVisual)),
            debug: true,
            skipUnregisteredReference: true);
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.DiplomaticallyFinished)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.StrengthOfSide)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleResultExplainers)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventResultsCalculated)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._eventTerrainType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._isFinishCalled)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._keepSiegeEvent)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventStartTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventResultsApplied)));
        // Collection of sides for the MapEvent
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), "_sides"));
    }
}
