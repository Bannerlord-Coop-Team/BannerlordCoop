using Common;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Issue availability and expiry are authoritative on the campaign server.</summary>
[HarmonyPatch]
internal static class IssueManagerClientLifecyclePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.DailyTick));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementEntered));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnSettlementLeft));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnCharacterPortraitPopUpOpened));
        yield return AccessTools.Method(typeof(IssueManager), nameof(IssueManager.OnHeroKilled));
    }

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
