using System;
using System.Collections.Generic;
using GameInterface.Services.Issues.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Generic;

public sealed record ModuleRescanCompletion<TIssue>(Func<Hero, bool> TryTriggerOwnedCompletion) where TIssue : IssueBase;

public interface IModuleRescanCompletionRunner
{
    void Run<TIssue>(ModuleRescanCompletion<TIssue> spec) where TIssue : IssueBase;
}

internal sealed class ModuleRescanCompletionRunner : IModuleRescanCompletionRunner
{
    private readonly IIssueOwnershipRegistry ownershipRegistry;

    public ModuleRescanCompletionRunner(IIssueOwnershipRegistry ownershipRegistry)
    {
        this.ownershipRegistry = ownershipRegistry;
    }

    public void Run<TIssue>(ModuleRescanCompletion<TIssue> spec) where TIssue : IssueBase
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
            if (kvp.Value is TIssue && ownershipRegistry.IsLocalPeerOwner(kvp.Key))
            {
                spec.TryTriggerOwnedCompletion(kvp.Key);
            }
        }
    }
}
