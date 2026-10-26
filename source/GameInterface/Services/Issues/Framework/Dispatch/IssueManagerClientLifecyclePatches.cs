using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Issue availability and expiry are authoritative on the campaign server.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal static class IssueManagerClientLifecyclePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementEntered));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementLeft));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnCharacterPortraitPopUpOpened));
    }

    [HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.OnHeroKilled))]
    private static class HeroKilledPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(Hero victim, out IDisposable __state)
        {
            __state = null;
            if (ModInformation.IsClient)
            {
                return false;
            }

            if (victim?.Issue == null)
            {
                return false;
            }

            if (!ContainerProvider.TryResolve<IIssueManagerLifecycleService>(out var lifecycle) ||
                !lifecycle.TryOpenOwnerScope(victim.Issue, out var scope))
            {
                return true;
            }

            __state = scope;
            return true;
        }

        [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
        [HarmonyPrefix]
        private static void DispatcherPrefix(
            Hero victim,
            Hero killer,
            TaleWorlds.CampaignSystem.Actions.KillCharacterAction.KillCharacterActionDetail detail,
            bool showNotification)
        {
            if (ModInformation.IsServer && victim?.Issue?.IssueQuest is { IsOngoing: true })
            {
                Campaign.Current.IssueManager.OnHeroKilled(victim, killer, detail, showNotification);
            }
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception, IDisposable __state)
        {
            __state?.Dispose();
            return __exception;
        }
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (ModInformation.IsServer)
        {
            return true;
        }

        return false;
    }
}
