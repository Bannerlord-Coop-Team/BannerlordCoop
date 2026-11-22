using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Dispatch;

/// <summary>Retains vanilla's last completion detail until the quest's outer completion call returns.</summary>
[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnQuestCompleted))]
internal class QuestCompletedDispatchPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestBase quest, QuestBase.QuestCompleteDetails detail)
    {
        if (ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
            dispatcher.ObserveQuestCompletion(quest, detail);
    }
}
