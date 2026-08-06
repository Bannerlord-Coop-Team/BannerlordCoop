using System;
using System.Collections.Generic;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public sealed record ModuleRescanCompletion<TIssue>(Func<Hero, bool> TryTriggerOwnedCompletion) where TIssue : IssueBase;

public static class ModuleRescanCompletionRunner
{
    public static void Run<TIssue>(ModuleRescanCompletion<TIssue> spec) where TIssue : IssueBase
    {
        if (spec?.TryTriggerOwnedCompletion == null) return;
        if (Campaign.Current?.IssueManager == null) return;

        // Snapshot first: a genuine completion inside the loop mutates IssueManager.Issues, and
        // MBReadOnlyDictionary's enumerator doesn't tolerate that.
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
