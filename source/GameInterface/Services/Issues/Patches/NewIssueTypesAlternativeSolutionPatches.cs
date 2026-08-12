using Common;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class NewIssueTypesAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(__instance.GetType())) return true;

        return (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) && ownershipRegistry.IsLocalPeerOwner(__instance.IssueOwner))
            || AlternativeSolutionCompletionAuthorityGuard.IsActive;
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal class NewIssueTypesAlternativeSolutionStartOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(__instance.GetType())) return true;

        return AlternativeSolutionStartAuthorityGuard.IsActive;
    }
}

[HarmonyPatch]
internal class NewIssueTypesAlternativeSolutionCompletionPatches
{
    [HarmonyPatch(typeof(LordNeedsHorsesIssueBehavior), nameof(LordNeedsHorsesIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LordNeedsHorsesRegisterEventsPostfix(LordNeedsHorsesIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(CapturedByBountyHuntersIssueBehavior), nameof(CapturedByBountyHuntersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void CapturedByBountyHuntersRegisterEventsPostfix(CapturedByBountyHuntersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandlordTrainingForRetainersIssueBehavior), nameof(LandlordTrainingForRetainersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandlordTrainingForRetainersRegisterEventsPostfix(LandlordTrainingForRetainersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(GangLeaderNeedsRecruitsIssueBehavior), nameof(GangLeaderNeedsRecruitsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsRecruitsRegisterEventsPostfix(GangLeaderNeedsRecruitsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior), nameof(LandLordNeedsManualLaborersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandLordNeedsManualLaborersRegisterEventsPostfix(LandLordNeedsManualLaborersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior), nameof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanVillageNeedsDraughtAnimalsRegisterEventsPostfix(HeadmanVillageNeedsDraughtAnimalsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior), nameof(LordNeedsGarrisonTroopsIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LordNeedsGarrisonTroopsRegisterEventsPostfix(LordNeedsGarrisonTroopsIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior), nameof(NearbyBanditBaseIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void NearbyBanditBaseRegisterEventsPostfix(NearbyBanditBaseIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior), nameof(LandLordTheArtOfTheTradeIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandLordTheArtOfTheTradeRegisterEventsPostfix(LandLordTheArtOfTheTradeIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior), nameof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RuralNotableInnAndOutRegisterEventsPostfix(SandBox.Issues.RuralNotableInnAndOutIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.ProdigalSonIssueBehavior), nameof(SandBox.Issues.ProdigalSonIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ProdigalSonRegisterEventsPostfix(SandBox.Issues.ProdigalSonIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.TheSpyPartyIssueQuestBehavior), nameof(SandBox.Issues.TheSpyPartyIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void TheSpyPartyRegisterEventsPostfix(SandBox.Issues.TheSpyPartyIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior), nameof(HeadmanNeedsGrainIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsGrainRegisterEventsPostfix(HeadmanNeedsGrainIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior), nameof(HeadmanNeedsToDeliverAHerdIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsToDeliverAHerdRegisterEventsPostfix(HeadmanNeedsToDeliverAHerdIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior), nameof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanCantSellProductsAtAFairPriceRegisterEventsPostfix(ArtisanCantSellProductsAtAFairPriceIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SmugglersIssueBehavior), nameof(SmugglersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void SmugglersRegisterEventsPostfix(SmugglersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior), nameof(ArtisanOverpricedGoodsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanOverpricedGoodsRegisterEventsPostfix(ArtisanOverpricedGoodsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void CaravanAmbushRegisterEventsPostfix(CaravanAmbushIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior), nameof(GangLeaderNeedsWeaponsIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsWeaponsRegisterEventsPostfix(GangLeaderNeedsWeaponsIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior), nameof(MerchantArmyOfPoachersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void MerchantArmyOfPoachersRegisterEventsPostfix(MerchantArmyOfPoachersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior), nameof(EscortMerchantCaravanIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void EscortMerchantCaravanRegisterEventsPostfix(EscortMerchantCaravanIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.RivalGangMovingInIssueBehavior), nameof(SandBox.Issues.RivalGangMovingInIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RivalGangMovingInRegisterEventsPostfix(SandBox.Issues.RivalGangMovingInIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.SnareTheWealthyIssueBehavior), nameof(SandBox.Issues.SnareTheWealthyIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void SnareTheWealthyRegisterEventsPostfix(SandBox.Issues.SnareTheWealthyIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    private static void OnHourlyTick()
    {
        if (Campaign.Current?.IssueManager == null) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;

        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(kvp.Value.GetType())) continue;
            if (!ownershipRegistry.IsLocalPeerOwner(kvp.Key)) continue;

            TryTriggerOwnedAlternativeSolutionCompletion(kvp.Value);
        }
    }

    private static void TryTriggerOwnedAlternativeSolutionCompletion(IssueBase issue)
    {
        AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion(issue.IssueOwner, RequestServerCompletion);
    }

    private static void RequestServerCompletion(Hero owner)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new RequestAlternativeSolutionCompletion(ownerId));
    }
}
