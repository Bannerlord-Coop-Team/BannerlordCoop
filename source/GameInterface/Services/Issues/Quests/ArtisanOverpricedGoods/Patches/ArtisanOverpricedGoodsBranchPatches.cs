using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtisanOverpricedGoods.Patches;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class ArtisanOverpricedGoodsBranchPatches
{
    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverItemsPartiallyOnConsequence))]
    [HarmonyPrefix]
    private static bool PartialPrefix(Quest __instance) => Before(__instance, ArtisanOverpricedGoodsBranch.PartialDelivery);

    [HarmonyPatch(typeof(Quest), nameof(Quest.DeliverItemsFullyOnConsequence))]
    [HarmonyPrefix]
    private static bool FullPrefix(Quest __instance) => Before(__instance, ArtisanOverpricedGoodsBranch.FullDelivery);

    [HarmonyPatch(typeof(Quest), nameof(Quest.AcceptCounterOffer))]
    [HarmonyPrefix]
    private static bool MerchantPrefix(Quest __instance) => Before(__instance, ArtisanOverpricedGoodsBranch.AcceptMerchantOffer);

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess))]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool SuccessPrefix(QuestBase __instance) =>
        !(__instance is Quest quest) || CallOriginalPolicy.IsOriginalAllowed() || quest._givenTradeGoods == quest._requestedTradeGoodAmount;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnCompleteWithSuccess))]
    [HarmonyPrefix]
    private static bool RewardsPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnFailed))]
    [HarmonyPrefix]
    private static bool FailedPrefix() => ModInformation.IsServer;

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;

    private static bool Before(Quest quest, ArtisanOverpricedGoodsBranch branch) =>
        !ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher) || dispatcher.BeforeQuestBranch(quest, (byte)branch);
}
