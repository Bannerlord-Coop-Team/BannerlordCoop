using Common;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Same bug/fix shape as <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/> (see that type's
/// doc comment for the full derivation - the same vanilla <c>IssueManager.DailyTick</c>/dedicated-host-no-
/// <c>Hero.MainHero</c> hazard applies identically here, since both issue types share the same base
/// <c>IssueBase.CompleteIssueWithAlternativeSolution</c>), scoped to
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/>. Deliberately its
/// own, independent prefix class rather than a change to
/// <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/> - Harmony ANDs multiple independent
/// prefixes on the same vanilla method, each deferring (returning true) for instances it doesn't own, so
/// stacking a second, type-scoped gate here is safe.
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class VillageNeedsCraftingMaterialsAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (__instance is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner);
    }
}

/// <summary>
/// Same shape as <see cref="VillageNeedsToolsAlternativeSolutionCompletionPatches"/>'s <c>HourlyTickEvent</c>
/// listener (see that type's doc comment for the full derivation of why an unconditional, ownership-self-
/// limiting listener is needed at all: vanilla's own ambient tick that would otherwise trigger this is
/// server-only, but the true owner is very often a remote client). Hooked off THIS issue type's own
/// <c>RegisterEvents</c> (the same already-allowlisted extension point), scanning only for
/// <see cref="VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue"/> instances.
/// </summary>
[HarmonyPatch(typeof(VillageNeedsCraftingMaterialsIssueBehavior))]
internal class VillageNeedsCraftingMaterialsAlternativeSolutionCompletionPatches
{
    [HarmonyPatch(nameof(VillageNeedsCraftingMaterialsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RegisterEventsPostfix(VillageNeedsCraftingMaterialsIssueBehavior __instance)
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (Campaign.Current?.IssueManager == null) return;
        if (!ContainerProvider.TryResolve<IVillageNeedsCraftingMaterialsIssueInterface>(out var issueInterface)) return;

        // Snapshot first: a genuine completion mutates IssueManager.Issues (removes the finalized entry), and
        // MBReadOnlyDictionary's own enumerator doesn't tolerate that mid-iteration.
        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (kvp.Value is VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue &&
                VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(kvp.Key))
            {
                issueInterface.TryTriggerOwnedAlternativeSolutionCompletion(kvp.Key);
            }
        }
    }
}
