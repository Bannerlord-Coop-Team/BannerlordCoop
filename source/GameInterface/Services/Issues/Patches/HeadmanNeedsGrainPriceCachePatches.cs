using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Standalone weekly grain-price broadcast mechanism (this quest's one genuinely new mechanism - no precedent
/// among any of the previously shipped issue types). <c>HeadmanNeedsGrainIssueBehavior._averageGrainPriceInCalradia</c>
/// is a BEHAVIOR-SINGLETON cache, not per-issue-instance state - it feeds <c>AlternativeSolutionNeededGold</c>
/// and the quest-solution dialogue's price text for EVERY <c>HeadmanNeedsGrainIssue</c> a hero might ever get,
/// for the whole lifetime of the campaign, recomputed via <c>WeeklyTick</c>/<c>OnGameLoadFinished</c>/
/// <c>OnNewGameCreatedPartialFollowUp</c> - all three of which do nothing except call the private
/// <c>CacheGrainPrice()</c>, making that one method the single choke point for all three triggers.
///
/// Deliberately NOT folded into the per-issue accept/creation capture pattern
/// (<see cref="Interfaces.SimpleIssueFactoryRegistry"/>): there is no Issue/Quest instance to attach this to at
/// the moment it changes - it updates on its own weekly cadence independent of whether any hero even has a
/// <c>HeadmanNeedsGrainIssue</c> active at all.
///
/// Mechanically: <c>CacheGrainPrice()</c> is blocked entirely on a client (never recomputes locally - avoids
/// relying on every client's own local market data being perfectly, instantaneously synced at the exact moment
/// a client's own ambient WeeklyTick happens to fire). Only a genuine SERVER-side recompute is captured (in the
/// postfix below) and published locally; <see cref="Handlers.HeadmanNeedsGrainPriceHandler"/> then broadcasts
/// it to every connected client, which force-writes the received value directly into its own local singleton
/// instance's private field (never recomputing its own). A newly (re)connecting client instead gets the
/// CURRENT authoritative value for free via <see cref="HeadmanNeedsGrainPricePersistencePatches"/> riding the
/// existing <c>TransferSaveState</c> full-save-transfer join flow - this broadcast only needs to cover
/// already-connected peers for the ongoing, in-session weekly update.
/// </summary>
[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior))]
internal class HeadmanNeedsGrainPriceCachePatches
{
    [HarmonyPatch("CacheGrainPrice")]
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;

    [HarmonyPatch("CacheGrainPrice")]
    [HarmonyPostfix]
    private static void Postfix(HeadmanNeedsGrainIssueBehavior __instance)
    {
        // The prefix above already means this postfix only ever runs following a genuine (real) computation
        // on the server - but guard again anyway in case some other caller ever invokes CacheGrainPrice
        // directly (defensive - matches this project's general style of not assuming Harmony's own gating is
        // the only path to a private method).
        if (ModInformation.IsClient) return;

        MessageBroker.Instance.Publish(__instance, new HeadmanGrainPriceCached(__instance._averageGrainPriceInCalradia));
    }
}
