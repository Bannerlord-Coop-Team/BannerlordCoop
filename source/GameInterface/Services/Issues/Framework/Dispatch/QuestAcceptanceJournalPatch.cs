using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

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

    [HarmonyPatch(typeof(ConversationSentence), nameof(ConversationSentence.RunConsequence))]
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowed())
            MessageBroker.Instance.Publish(typeof(QuestAcceptanceJournalPatch), new QuestAcceptanceFinished(null));
    }
}
