using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Gates the three vanilla entry points that actually generate a fresh issue
/// (<c>IssuesCampaignBehavior.DailyTickClan</c>, <c>OnSettlementDailyTick</c> and
/// <c>OnNewGameCreatedPartialFollowUpEnd</c> - see <c>IssueManager.CheckForIssues</c>/
/// <c>CreateNewIssue</c> callers) to the server only. All three roll ambient, unseeded randomness
/// directly (generation-chance rolls via <c>MBRandom.RandomFloat</c>, candidate selection via
/// <c>MBRandom.ChooseWeighted</c>, and a plain <c>System.Random</c> settlement shuffle in the new-game
/// path) to decide WHICH hero gets a new issue and WHEN. If every client ran this locally, each would make
/// a different random choice and the campaign would diverge - the same desync shape already hit and fixed
/// elsewhere in this project for other ambient-Rand call sites. The resulting
/// <c>VillageNeedsToolsIssue</c> creation is captured and replicated by
/// <see cref="IssueManagerCreateNewIssuePatches"/> instead of letting clients re-derive it locally.
///
/// <c>OnSessionLaunched</c> is deliberately left unpatched: it only calls <c>AddDialogues</c> (every
/// human-player client needs the issue dialogue lines registered locally for its own conversation UI,
/// consistent with this project's conversations-run-locally philosophy) plus a settlement shuffle whose
/// result is never read again in the decompiled source - harmless to run everywhere.
/// </summary>
[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal class IssuesCampaignBehaviorGenerationPatches
{
    [HarmonyPatch("DailyTickClan")]
    [HarmonyPrefix]
    private static bool DailyTickClanPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnSettlementDailyTick")]
    [HarmonyPrefix]
    private static bool OnSettlementDailyTickPrefix() => ModInformation.IsServer;

    [HarmonyPatch("OnNewGameCreatedPartialFollowUpEnd")]
    [HarmonyPrefix]
    private static bool OnNewGameCreatedPartialFollowUpEndPrefix() => ModInformation.IsServer;
}
