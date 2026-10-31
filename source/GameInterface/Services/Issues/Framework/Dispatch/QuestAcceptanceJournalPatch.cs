using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch]
internal class QuestAcceptanceJournalPatch
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
    [HarmonyPrefix]
    private static void StartPrefix(QuestBase __instance)
    {
        if (ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowed())
            MessageBroker.Instance.Publish(__instance, new QuestAcceptanceStarted(__instance));
    }
}
