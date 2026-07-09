using GameInterface.AutoSync;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;

namespace GameInterface.Services.Towns;
internal class TownSync : IAutoSync
{
    public TownSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._prosperity)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._isCastle)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.GarrisonAutoRecruitmentIsEnabled)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._ownerClan)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._tradeBoundVillagesCache)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.Buildings)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BuildingsInProgress)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.BoostBuildingProcess)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town.InRebelliousState)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Town), nameof(Town._marketData))); // readonly

        //// Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Governor)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.TradeTaxAccumulated)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Security)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Loyalty)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.LastCapturedBy)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Town), nameof(Town.Workshops)));

        //// Target Methods
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.ChangeCurrentBuildingQueue)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingHelper), nameof(BuildingHelper.BoostBuildingProcessWithGold)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(ClanVariablesCampaignBehavior), nameof(ClanVariablesCampaignBehavior.UpdateClanSettlementAutoRecruitment)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.TickCurrentBuildingForTown)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildArtisanWorkshop)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopForHeroAtGameStart)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(WorkshopsCampaignBehavior), nameof(WorkshopsCampaignBehavior.BuildWorkshopsAtGameStart)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.ApplyRebellionConsequencesToSettlement)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.CheckAndSetTownRebelliousState)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(RebellionsCampaignBehavior), nameof(RebellionsCampaignBehavior.OnGameLoaded)));
        AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(ClanPartyItemVM), nameof(ClanPartyItemVM.OnAutoRecruitChanged)));
        //AutoSyncRegistry.AddTargetMethod(typeof(Town), AccessTools.Method(typeof(BuildingsCampaignBehavior), nameof(BuildingsCampaignBehavior.BuildDevelopmentsAtGameStart))); // TODO: Verify why this breaks or if its even necessary
    }
}
