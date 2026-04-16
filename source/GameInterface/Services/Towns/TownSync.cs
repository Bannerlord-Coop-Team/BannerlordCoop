using GameInterface.DynamicSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Towns;
internal class TownSync : IDynamicSync
{
    public TownSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        //// Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._prosperity)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._isCastle)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.GarrisonAutoRecruitmentIsEnabled)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._ownerClan)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._security)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._loyalty)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeBoundVillagesCache)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.Buildings)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BoostBuildingProcess)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeTax)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.InRebelliousState)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._marketData))); // readonly

        //// Properties
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.TradeTaxAccumulated)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Security)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Loyalty)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.LastCapturedBy)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Workshops)));

        //// Target Methods
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuildingQueue)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.BoostBuildingProcessWithGold)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.UpdateClanSettlementAutoRecruitment)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.TickCurrentBuildingForTown)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildArtisanWorkshop)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopForHeroAtGameStart)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.ApplyRebellionConsequencesToSettlement)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.CheckAndSetTownRebelliousState)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.OnGameLoaded)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(ClanPartyItemVM), nameof(ClanPartyItemVM.OnAutoRecruitChanged)));
        //dynamicSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart))); // TODO: Verify why this breaks or if its even necessary
    }
}
