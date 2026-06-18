using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;
internal class SettlementSync : IAutoSync
{
    public SettlementSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Culture)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.HasVisited)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Hideout)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.LastVisitTimeOfOwner)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.MilitiaPartyComponent)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Town)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Village)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._isVisible)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._lastAttackerParty)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._name)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._nextLocatable)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.NumberOfLordPartiesTargeting)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._position)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._readyMilitia)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._boundVillages)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._heroesWithoutPartyCache)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._locatorNodeIndex)));

        // Certain MBLists aren't being registered correctly, waiting on a fix for certain collections with dynamic sync
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._notablesCache)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._partiesCache)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._settlementWallSectionHitPointsRatioList)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement._siegeEngineMissiles)));
        //AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.Alleys)));

        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(Settlement), nameof(Settlement.Stash))); // readonly

        //// Properties
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.Party)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.BribePaid)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.SiegeEvent)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.IsActive)));
        //AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.IsVisible)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.CurrentSiegeState)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.GatePosition)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.LocationComplex))); // Might not be needed

        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyLandThreatIntensity)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyNavalThreatIntensity)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyLandAllyIntensity)));
        AutoSyncRegistry.AddProperty(AccessTools.Property(typeof(Settlement), nameof(Settlement.NearbyNavalAllyIntensity)));

        //// Target Methods
        AutoSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(SettlementClaimantCampaignBehavior), nameof(SettlementClaimantCampaignBehavior.OnSettlementOwnerChanged)));
        AutoSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnInitialize)));
        AutoSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(MilitiaPartyComponent), nameof(MilitiaPartyComponent.OnFinalize)));
        AutoSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(EnterSettlementAction), nameof(EnterSettlementAction.ApplyInternal)));
        AutoSyncRegistry.AddTargetMethod(typeof(Settlement), AccessTools.Method(typeof(PlayerTownVisitCampaignBehavior), nameof(PlayerTownVisitCampaignBehavior.OnSettlementEntered)));
    }
}
