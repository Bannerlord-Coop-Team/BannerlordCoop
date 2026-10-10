using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal))]
internal class CompleteQuestWithBetrayalDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, out IssueBase __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestOutcome(__instance, IssueOutcome.QuestBetrayal, out __state);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __state)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.After(__state, IssueOutcome.QuestBetrayal);
    }
}
