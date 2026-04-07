using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides
{
    internal class MapEventSideSync : IDynamicSync
    {
        public MapEventSideSync(DynamicSyncRegistry autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.CasualtyStrength)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.LeaderParty)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.MissionSide)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.TroopCasualties)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.InfluenceValue)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.IsSurrendered)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.LeaderSimulationModifier)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.RenownValue)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.StrengthRatio)));
            //autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent)));
            
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._requiresTroopCacheUpdate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroop)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroopIndex)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapFaction)));

            // Not supported
            // autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapFaction)));
        }
    }
}
