using GameInterface.AutoSync;
using GameInterface.AutoSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings
{
    internal class BuildingSync : IAutoSync
    {
        public BuildingSync(AutoSyncRegistry AutoSyncRegistry)
        {
            // Fields
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building._hitpoints)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building._currentLevel)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building.IsCurrentlyDefault)));
            AutoSyncRegistry.AddField(AccessTools.Field(typeof(Building), nameof(Building.BuildingProgress)));

            // Properties
            AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Building), nameof(Building.Town)));

            // Targetmethods
            AutoSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeDefaultBuilding)));
            AutoSyncRegistry.AddTargetMethod(typeof(Building), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart)));
        }
    }
}
