using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings
{
    internal class BuildingSync : IDynamicSync
    {
        public BuildingSync(DynamicSyncRegistry dynamicSyncRegistry)
        {
            // Fields
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building._hitpoints)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building._currentLevel)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building.IsCurrentlyDefault)));
            dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building.BuildingProgress)));

            // Properties
            dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Building), nameof(Building.Town)));

            // Targetmethods
            dynamicSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(Town), nameof(Town.TickCurrentBuilding)));
            dynamicSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeDefaultBuilding)));
            dynamicSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart)));
        }
    }
}
