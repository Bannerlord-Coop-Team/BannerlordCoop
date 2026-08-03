using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Gates <see cref="IssueManager"/>'s two ambient ticks so an already-existing issue's timers/timeouts are
/// only ever rolled once (server-authoritative) instead of independently per client.
///
/// <see cref="IssueManager.DailyTick"/> rolls a 0.2f random-timeout chance per ongoing-without-quest issue,
/// and (for an alternative-solution issue whose troops are due back) further ambient rolls for wound chance,
/// XP distribution and troop count. Both are real per-client-divergence risks if left to run everywhere, so
/// this is gated to the server only, exactly like <see cref="IssuesCampaignBehaviorGenerationPatches"/>.
///
/// Known limitation (not solved here, flagged for follow-up): the alternative-solution troop-return branch
/// inside vanilla DailyTick also dereferences <c>Hero.MainHero</c>/<c>MobileParty.MainParty</c> directly
/// (to pay out gold and return troops) - both null on a dedicated host with no main hero. Solving that
/// properly needs an explicit "who accepted this alternative solution" ownership model this vanilla feature
/// was never designed with (the same category of gap as e.g. <c>PlayerEncounterSync.md</c>'s documented
/// leaderless-party gap). Rather than leave that a live server crash, the finalizer below swallows and logs
/// any exception from the tick instead of letting it kill the process; a day where this triggers still loses
/// the rest of that day's issue-timeout processing, which is an accepted, documented trade-off for this
/// scoped prototype, not a full fix.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerTickPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<IssueManagerTickPatches>();

    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyPrefix]
    private static bool DailyTickPrefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return ModInformation.IsServer;
    }

    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyFinalizer]
    private static Exception DailyTickFinalizer(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception,
                "IssueManager.DailyTick threw on the server; swallowing to keep the server tick alive " +
                "(likely the alternative-solution reward/return path's Hero.MainHero dependency - see " +
                "the type doc comment for the known, unsolved limitation).");
        }
        return null;
    }

    // The dedicated host has no MobileParty.MainParty; HourlyTick's CheckIfTroopsCanReturnToMainParty
    // dereferences it directly and would NRE every in-game hour whenever any alternative-solution issue has
    // troops pending return. VillageNeedsToolsIssue/Quest's own per-issue HourlyTick override is a no-op
    // (see the decompiled source), so nothing meaningful is lost by skipping the whole dispatch server-side;
    // each connected client still runs it for its own MainParty.
    //
    // Known limitation (not solved here): _awaitingAlternativeSolutionTroops is a single IssueManager-level
    // roster, not keyed per player, so if multiple connected players simultaneously have alternative-solution
    // troops pending, each client's own local HourlyTick could try to claim the same shared roster onto its
    // own MainParty. Solving this needs the same "who accepted this alternative solution" ownership model
    // called out on DailyTick above; out of scope for this prototype.
    [HarmonyPatch(nameof(IssueManager.HourlyTick))]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix() => ModInformation.IsClient;
}
