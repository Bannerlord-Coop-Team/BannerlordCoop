using Common;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug 2 fix: nothing previously recorded which specific connected player's client genuinely accepted a
/// <c>VillageNeedsToolsIssue</c>'s alternative solution, so vanilla's own ambient <c>IssueManager.DailyTick</c>
/// - which only ever runs on the server (see <see cref="IssueManagerTickPatches"/>) - would eventually see
/// a non-owner mirror copy's <c>AlternativeSolutionReturnTimeForTroops</c> (left at its uninitialized,
/// always-in-the-past default by <see cref="VillageNeedsToolsIssueInterface.MirrorAlternativeAccepted"/>,
/// which deliberately never sets it - see that method's own doc comment) and call the real
/// <c>CompleteIssueWithAlternativeSolution()</c> on the SERVER's own copy: no real troops, so the wrong
/// party gets paid/no one does, and on a dedicated host with no <c>Hero.MainHero</c> this NREs every day
/// forever (see <see cref="IssueManagerTickPatches"/>'s documented, previously-accepted limitation).
///
/// DEVIATION FROM THE ORIGINAL DESIGN DOC (kept per the adversarial review's Finding 1, which is correct):
/// the original plan was to have <c>MirrorAlternativeAccepted</c> permanently pin
/// <c>AlternativeSolutionReturnTimeForTroops</c> to <c>CampaignTime.Never</c> on every non-owner mirror
/// (plus seed a placeholder <c>JournalLog</c> so <c>DailyTick</c>'s cosmetic "else" branch wouldn't index an
/// empty <c>JournalEntries</c>). That was rejected: per
/// <c>Coop.Core.Server.Connections.States.TransferSaveState</c>, EVERY (re)connection - not just a fresh
/// join - is serviced by the server calling <c>SaveCurrentGame()</c> on its own live state and shipping that
/// snapshot to the connecting peer. Since the server's own copy of a remotely-accepted issue IS one of
/// these "poisoned" mirrors, an ordinary disconnect+reconnect by the true accepter would silently overwrite
/// their own real, in-flight due date with the permanent poison value - a silent, permanent soft-lock of
/// their real troops/reward reachable through completely routine multiplayer churn, strictly worse than the
/// original bug. Gating the CONSEQUENCE instead (below) - never touching
/// <c>AlternativeSolutionReturnTimeForTroops</c> at all - means a reconnect always restores the true,
/// truthful value everywhere, and also means <c>DailyTick</c>'s cosmetic "else"/journal branch is simply
/// never reached for a mirror (its <c>IsPast</c> check stays true forever, same as today), so the
/// placeholder-journal-entry workaround the original plan needed is no longer necessary either.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class VillageNeedsToolsAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        // Scoped to this one issue type only - every other IssueBase subtype is already disabled from ever
        // being created (see DisableAllIssueBehaviorsExceptAllowlist), but stay explicit rather than implicit.
        if (__instance is not VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue) return true;

        // Skip the real consequence everywhere except the one machine that is the recorded owner (a
        // dedicated server, or any other non-owning peer, always reads false here). This is the ONLY gate
        // needed: it covers vanilla's own DailyTick call (server-only, see IssueManagerTickPatches) AND the
        // genuine owner-triggered call below (VillageNeedsToolsIssueInterface.TryTriggerOwnedAlternativeSolutionCompletion),
        // which only ever runs where IsLocalPeerOwner is already true.
        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner);
    }
}

/// <summary>
/// The other half of the Bug 2 fix: since vanilla's <c>IssueManager.DailyTick</c> only ever runs on the
/// server (see <see cref="IssueManagerTickPatches"/>), and the true accepter is very often a remote CLIENT
/// (whose own <c>DailyTick</c> is unconditionally blocked there), nothing would ever call the real
/// <c>CompleteIssueWithAlternativeSolution()</c> on the one machine (the owner's own) that actually has real
/// troops/hero state to run it against. This registers an unconditional (no <c>IsServer</c>/<c>IsClient</c>
/// gate at all - self-limiting purely by the ownership check) <c>HourlyTickEvent</c> listener that checks
/// every <c>VillageNeedsToolsIssue</c> the LOCAL peer owns for a past due date and triggers the genuine
/// completion directly on that one machine. Hooked off <c>VillageNeedsToolsIssueBehavior.RegisterEvents</c>
/// (the same already-allowlisted extension point <see cref="DisableAllIssueBehaviorsExceptAllowlist"/> relies
/// on), using the behavior instance itself as the listener owner - the same pattern already used by e.g.
/// <c>DisableRecruitmentCampaignBehavior</c>.
///
/// Checked against no other issue type ever reaching this scan (the <c>is VillageNeedsToolsIssue</c> filter),
/// and against firing more than once for the same day (idempotent - <see cref="IssueBase.IsSolvingWithAlternative"/>
/// is no longer true once the owner's own call above finalizes the issue, so a subsequent tick's snapshot
/// simply won't see it in <c>IssueManager.Issues</c> anymore).
/// </summary>
[HarmonyPatch(typeof(VillageNeedsToolsIssueBehavior))]
internal class VillageNeedsToolsAlternativeSolutionCompletionPatches
{
    [HarmonyPatch(nameof(VillageNeedsToolsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(VillageNeedsToolsIssueBehavior __instance)
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (Campaign.Current?.IssueManager == null) return;
        if (!ContainerProvider.TryResolve<IVillageNeedsToolsIssueInterface>(out var issueInterface)) return;

        // Snapshot first: a genuine completion mutates IssueManager.Issues (removes the finalized entry),
        // and MBReadOnlyDictionary's own enumerator doesn't tolerate that mid-iteration.
        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (kvp.Value is VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue &&
                VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(kvp.Key))
            {
                issueInterface.TryTriggerOwnedAlternativeSolutionCompletion(kvp.Key);
            }
        }
    }
}
