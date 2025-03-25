using GameInterface.AutoSync;
using GameInterface.DynamicSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownDynamicSync : IDynamicSync
{
    public TownDynamicSync(IDynamicSyncBuilder dynamicSyncBuilder)
    {
        dynamicSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));
        dynamicSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Security)));
        dynamicSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Loyalty)));
        dynamicSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.LastCapturedBy)));

        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._prosperity)));
        // TODO: Find out why these 2 values Break Harmony
        //dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._wallLevel)));
        //dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._isCastle)));
        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._ownerClan)));
        // Not synced fields
        // autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._security)));
        // autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._loyalty)));
        // Remove Prop or auto sync
        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.BoostBuildingProcess)));
        // Remove Prop or auto sync
        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeTax)));
        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.InRebelliousState)));
        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._governor)));

        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.Buildings)));

        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress)));

        dynamicSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeBoundVillagesCache)));

        dynamicSyncBuilder.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Workshops)));


        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuildingQueue)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuilding)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildArtisanWorkshop)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopForHeroAtGameStart)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.DecideProject)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.ApplyRebellionConsequencesToSettlement)));
        dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.CheckAndSetTownRebelliousState)));

        // TODO: Find out why patching this breaks Harmony
        //dynamicSyncBuilder.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart)));
    }
}
