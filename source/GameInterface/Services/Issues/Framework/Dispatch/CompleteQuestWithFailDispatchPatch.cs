using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail))]
internal class CompleteQuestWithFailDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, TextObject cancelLog, out IssueBase __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestOutcome(__instance, IssueOutcome.QuestFail, out __state, cancelLog);
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __state)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.After(__state, IssueOutcome.QuestFail);
    }
}
