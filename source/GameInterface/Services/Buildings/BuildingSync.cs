using GameInterface.AutoSync;
using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings
{
    internal class BuildingSync : IAutoSync
    {
        public BuildingSync(IAutoSyncBuilder autoSyncBuilder)
        {
            autoSyncBuilder.AddField(AccessTools.Field(typeof(Building), nameof(Building._hitpoints)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(Building), nameof(Building._currentLevel)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(Building), nameof(Building.IsCurrentlyDefault)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeDefaultBuilding)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart)));

            autoSyncBuilder.AddField(AccessTools.Field(typeof(Building), nameof(Building.BuildingProgress)));
            autoSyncBuilder.AddFieldChangeMethod(AccessTools.Method(typeof(Town), nameof(Town.TickCurrentBuilding)));

            autoSyncBuilder.AddProperty(AccessTools.Property(typeof(Building), nameof(Building.Town)));
        }
    }
}
