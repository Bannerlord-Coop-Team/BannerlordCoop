using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class SettlementSync : IDynamicSync
{
    public SettlementSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        //// Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._numberOfLordPartiesAt)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._boundVillages)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._heroesWithoutPartyCache)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._locatorNodeIndex)));

        // Certain MBLists aren't being registered correctly, waiting on a fix for certain collections with dynamic sync
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._notablesCache)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._partiesCache)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._settlementWallSectionHitPointsRatioList)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._siegeEngineMissiles)));
        //dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.Alleys)));

        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash))); // readonly

        //// Properties
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.Party)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.BribePaid)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.IsActive)));
        //dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.IsVisible)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.CurrentSiegeState)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.GatePosition)));

        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyLandThreatIntensity)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyNavalThreatIntensity)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyLandAllyIntensity)));
        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyNavalAllyIntensity)));

        //// Target Methods
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));
        dynamicSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(KingdomManager), nameof(KingdomManager.UpdateLordPartyVariablesRelatedToSettlements)));
    }
}
