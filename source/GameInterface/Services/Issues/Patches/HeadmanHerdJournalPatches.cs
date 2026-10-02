using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog), typeof(TextObject), typeof(bool))]
internal static class HeadmanHerdJournalPatches
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, TextObject text, bool hideInformation)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance is Quest quest)
            MessageBroker.Instance.Publish(quest, new HeadmanHerdJournalAdded(quest, text, hideInformation));
    }
}

[HarmonyPatch(typeof(Quest))]
// Compile these callers after settlement sync has patched their small setters.
[HarmonyPatchCategory(GameInterface.HARMONY_GAME_STARTED_CATEGORY)]
internal static class HeadmanHerdReceivedConsequencePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnCompleteWithSuccess));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnFailed));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnTimedOut));
        yield return AccessTools.DeclaredMethod(typeof(Quest), nameof(Quest.OnCanceled));
    }

    [HarmonyPrefix]
    internal static bool Prefix() =>
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsServer;
}
