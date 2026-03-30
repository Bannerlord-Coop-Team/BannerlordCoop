using GameInterface.DynamicSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns;
internal class TownSync : IDynamicSync
{
    public TownSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        // Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._prosperity)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._isCastle)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._ownerClan)));
        //// Not synced fields
        //// autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._security)));
        //// autoSyncBuilder.AddField(AccessTools.Field(typeof(Town), nameof(Town._loyalty)));
        //// Remove Prop or auto sync
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BoostBuildingProcess)));
        //// Remove Prop or auto sync
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeTax)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.InRebelliousState)));
        
        // Already handled via property
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._governor)));

        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.Buildings)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeBoundVillagesCache)));

        // Properties
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Security)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Loyalty)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.LastCapturedBy)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Workshops)));

        // Targetmethods
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuildingQueue)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildArtisanWorkshop)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopForHeroAtGameStart)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.ApplyRebellionConsequencesToSettlement)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.CheckAndSetTownRebelliousState)));
        // TODO: Verify why this breaks or if its even necessary
        //dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart)));
    }
}
