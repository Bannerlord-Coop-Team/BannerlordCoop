using HarmonyLib;
using Helpers;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace ServerHeadless.Bootstrap.Patches
{
    /// <summary>
    /// Guards for campaign-behavior OnGameLoaded handlers that build caches assuming invariants that
    /// don't hold for an arbitrary save loaded headlessly. These caches are rebuilt during play, so
    /// skipping the eager load-time build is safe.
    /// </summary>
    [HarmonyPatch]
    internal class BehaviorPatches
    {
        // Builds a dictionary keyed only by fortification/village settlements, then indexes it by each
        // lord party's TargetSettlement — which can be a settlement not in the dict (KeyNotFound).
        [HarmonyPatch(typeof(AiVisitSettlementBehavior), "RefreshTheTargetingSettlementDictionary")]
        [HarmonyPrefix]
        static bool RefreshTheTargetingSettlementDictionaryPrefix() => false;

        // Caches bandit counts per hideout; trips over bandit parties whose home settlement doesn't
        // resolve cleanly for this save.
        [HarmonyPatch(typeof(BanditSpawnCampaignBehavior), "CacheBanditCounts")]
        [HarmonyPrefix]
        static bool CacheBanditCountsPrefix() => false;

        // Rebuilds tournament prizes on load; indexes a prize list that can be empty for this save.
        [HarmonyPatch(typeof(TournamentCampaignBehavior), "OnGameLoaded")]
        [HarmonyPrefix]
        static bool TournamentOnGameLoadedPrefix() => false;

        // Each active issue re-initializes on load (InitializeIssueBaseOnLoad -> OnGameLoad). Some
        // issue/quest types fault re-establishing their state for an arbitrary headless-loaded save.
        // Swallow per-issue failures so one broken issue doesn't abort the whole load (finalizer
        // returning null suppresses the exception).
        [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.InitializeIssueBaseOnLoad))]
        [HarmonyFinalizer]
        static Exception InitializeIssueBaseOnLoadFinalizer() => null;

        // Averages an item's price across all town/village markets; divides by the market count,
        // which can be zero for some items/world states during load. Return 0 instead of throwing.
        [HarmonyPatch(typeof(QuestHelper), nameof(QuestHelper.GetAveragePriceOfItemInTheWorld))]
        [HarmonyFinalizer]
        static Exception GetAveragePriceOfItemInTheWorldFinalizer(ref int __result, Exception __exception)
        {
            if (__exception != null) __result = 0;
            return null;
        }
    }
}
