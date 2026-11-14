using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventComponents;

internal class MapEventComponentSync : IAutoSync
{
    public MapEventComponentSync(AutoSyncRegistry autoSyncBuilder)
    {
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventComponent), nameof(MapEventComponent.MapEvent)));

        autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventComponent), nameof(MapEventComponent._isFinished)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(HideoutEventComponent), nameof(HideoutEventComponent.IsSendTroops)));

        // The battle settlement, the battle type and the siege outcome live on the components
        // (MapEventSettlement, _mapEventType, _keepSiegeEvent), not on MapEvent.
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventComponent), nameof(MapEventComponent.MapEventSettlement)));
        autoSyncBuilder.AddProperty(AccessTools.Property(typeof(HideoutEventComponent), nameof(HideoutEventComponent.InitialHideoutPopulation)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent._eventType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent._keepSiegeEvent)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent._isSiegeCompleted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeAssaultEventComponent), nameof(SiegeAssaultEventComponent._isSiegeWon)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeSallyOutEventComponent), nameof(SiegeSallyOutEventComponent._isSiegeCompleted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeSallyOutEventComponent), nameof(SiegeSallyOutEventComponent._isSiegeWon)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeOutsideEventComponent), nameof(SiegeOutsideEventComponent._eventType)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(SiegeOutsideEventComponent), nameof(SiegeOutsideEventComponent._isSiegeCompleted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(BlockadeBattleEventComponent), nameof(BlockadeBattleEventComponent._isSallyOut)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(BlockadeBattleEventComponent), nameof(BlockadeBattleEventComponent._isSiegeCompleted)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(BlockadeBattleEventComponent), nameof(BlockadeBattleEventComponent._isSiegeWon)));
        autoSyncBuilder.AddField(AccessTools.Field(typeof(FieldBattleEventComponent), nameof(FieldBattleEventComponent._isNavalEncounterFinishedWithDisengage)));
    }
}
