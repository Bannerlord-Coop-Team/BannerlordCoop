using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
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
    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnHeroKilled))]
    private static class HeroKilledPatch
    {
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
