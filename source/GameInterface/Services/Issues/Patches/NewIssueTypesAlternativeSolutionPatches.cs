using Common;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.RegisterEvents))]
internal class NewIssueTypesAlternativeSolutionCompletionPatches
{
    private static readonly ConditionalWeakTable<IssuesCampaignBehavior, object> listenerRegistered = new();

    [HarmonyPostfix]
    private static void RegisterEventsPostfix(IssuesCampaignBehavior __instance)
    {
        if (listenerRegistered.TryGetValue(__instance, out _)) return;
        listenerRegistered.Add(__instance, null);

        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);
    }

    private static void OnHourlyTick()
    {
        if (Campaign.Current?.IssueManager == null) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;

        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (QuestTypeRegistry.Get(kvp.Value)?.SupportsAlternativeAccept != true) continue;
            if (!ownershipRegistry.IsLocalPeerOwner(kvp.Key)) continue;

            TryTriggerOwnedAlternativeSolutionCompletion(kvp.Value);
        }
    }

    private static void TryTriggerOwnedAlternativeSolutionCompletion(IssueBase issue)
    {
        AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion(issue.IssueOwner, RequestServerCompletion);
    }

    private static void RequestServerCompletion(Hero owner)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new RequestAlternativeSolutionCompletion(ownerId));
    }
}
