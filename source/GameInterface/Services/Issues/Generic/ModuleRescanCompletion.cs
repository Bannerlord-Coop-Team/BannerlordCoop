using System;
using System.Collections.Generic;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

/// <summary>
/// Ground truth: <c>VillageNeedsCraftingMaterialsAlternativeSolutionCompletionPatches.OnHourlyTick</c>/
/// <c>VillageNeedsToolsAlternativeSolutionCompletionPatches</c>'s equivalent listener - both hook their own
/// type's <c>RegisterEvents</c> to add an unconditional <c>HourlyTickEvent</c> listener that snapshots every
/// ongoing issue of this behavior's type, and for whichever ones the local peer owns, tries to trigger genuine
/// completion. Exists because vanilla's own ambient tick that would otherwise trigger this is server-only, but
/// the true owner is very often a remote client.
/// </summary>
public sealed record ModuleRescanCompletion<TIssue>(Func<Hero, bool> TryTriggerOwnedCompletion) where TIssue : IssueBase;

/// <summary>
/// Runs a <see cref="ModuleRescanCompletion{TIssue}"/> spec. Snapshot-then-scan (a genuine completion inside the
/// loop mutates <c>IssueManager.Issues</c>, which <c>MBReadOnlyDictionary</c>'s own enumerator does not
/// tolerate mid-iteration - see the real precedent's own comment) every ongoing issue of
/// <typeparamref name="TIssue"/> the local peer owns.
/// </summary>
public static class ModuleRescanCompletionRunner
{
    public static void Run<TIssue>(ModuleRescanCompletion<TIssue> spec) where TIssue : IssueBase
    {
        if (spec?.TryTriggerOwnedCompletion == null) return;
        if (Campaign.Current?.IssueManager == null) return;

        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (kvp.Value is TIssue && VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(kvp.Key))
            {
                spec.TryTriggerOwnedCompletion(kvp.Key);
            }
        }
    }
}
