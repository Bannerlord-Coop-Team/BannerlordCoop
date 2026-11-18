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

    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}
