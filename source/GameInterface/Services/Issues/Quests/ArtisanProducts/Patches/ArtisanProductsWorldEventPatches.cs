using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Issues;
using Behavior = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanProducts.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ArtisanProductsWorldEventPatches
{
    [HarmonyPatch(typeof(Behavior), nameof(Behavior.OnCheckForIssue))]
    [HarmonyPrefix]
    private static bool Generation() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Behavior), nameof(Behavior.SelectTargetSettlement))]
    [HarmonyPrefix]
    private static bool Target(Hero issueGiver, ref Settlement __result)
    {
        if (ContainerProvider.TryResolve<ArtisanProductsCreationCaptureStrategy>(out var creation) &&
            creation.TryGetCreation(issueGiver, out var target, out _))
        {
            __result = target;
            return false;
        }
        // Vanilla searches from MainParty, which the dedicated server does not have.
        __result = ArtisanProductsCreationCaptureStrategy.SelectTargetSettlement(issueGiver);
        return false;
    }

    [HarmonyPatch(typeof(Behavior), nameof(Behavior.SelectCounterOfferHero))]
    [HarmonyPrefix]
    private static bool Merchant(Hero issueGiver, ref Hero __result)
    {
        if (!ContainerProvider.TryResolve<ArtisanProductsCreationCaptureStrategy>(out var creation) ||
            !creation.TryGetCreation(issueGiver, out _, out var merchant)) return true;
        __result = merchant;
        return false;
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.BeforeGameMenuOpened))]
    [HarmonyPrefix]
    private static bool Menu() => ModInformation.IsClient;
}

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ArtisanProductsServerEventPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> Targets()
    {
        yield return AccessTools.Method(typeof(Quest), nameof(Quest.OnHeroKilled));
    }

    [HarmonyPrefix]
    private static bool Before() => ModInformation.IsServer;
}

