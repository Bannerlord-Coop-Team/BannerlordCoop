using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

internal class MapEventSync : IAutoSync
{
    public MapEventSync(IAutoSyncBuilder autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.AttackersRanAway)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.BattleState)));
        //autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Component)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsInvulnerable)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsPlayerSimulation)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.IsVisible)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.MapEventSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.Position)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEvent), nameof(MapEvent.State)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.DiplomaticallyFinished)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.PlayerCaptured)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent.SimulationContext)));
        //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleResultExplainers)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleResultsCalculated)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleResultsCommitted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._battleState)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._eventTerrainType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._isFinishCalled)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._isVisible)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._keepSiegeEvent)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventStartTime)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEvent), nameof(MapEvent._mapEventUpdateCount)));
    }
}