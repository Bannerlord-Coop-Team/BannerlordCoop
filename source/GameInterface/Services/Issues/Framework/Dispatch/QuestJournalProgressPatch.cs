using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Observes UI progress that the unstarted authoritative quest does not calculate.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(JournalLog), nameof(JournalLog.UpdateCurrentProgress))]
internal class QuestJournalProgressPatch
{
    [HarmonyPrefix]
    private static void Prefix(JournalLog __instance, out int __state) => __state = __instance.CurrentProgress;

    [HarmonyPostfix]
    private static void Postfix(JournalLog __instance, int __state)
    {
        if (ModInformation.IsClient && !QuestJournalReplay.IsActive && __instance.CurrentProgress != __state)
            MessageBroker.Instance.Publish(__instance, new QuestJournalProgressChanged(__instance));
    }
}
