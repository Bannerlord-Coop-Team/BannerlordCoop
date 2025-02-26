using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEventSides
{
    internal class MapEventSideSync : IAutoSync
    {
        public MapEventSideSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.CasualtyStrength)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.LeaderParty)));
            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(MapEventSide), nameof(MapEventSide.MissionSide)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.Casualties)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.InfluenceValue)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.IsSurrendered)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.LeaderSimulationModifier)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.RenownAtMapEventEnd)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.RenownValue)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide.StrengthRatio)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapEvent)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._mapFaction)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._requiresTroopCacheUpdate)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroop)));
            autoSyncBuilder.AddField(AccessTools.Field(typeof(MapEventSide), nameof(MapEventSide._selectedSimulationTroopIndex)));
        }
    }
}
