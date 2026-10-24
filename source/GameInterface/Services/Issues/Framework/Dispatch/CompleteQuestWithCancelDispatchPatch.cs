using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel))]
internal class CompleteQuestWithCancelDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, TextObject cancelLog, out IssueBase __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestOutcome(__instance, IssueOutcome.QuestCancel, out __state, cancelLog);
    }

    [HarmonyFinalizer]
    private static void Finalizer(QuestBase __instance, IssueBase __state, Exception __exception)
    {
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return;
        }

        dispatcher.AfterQuestOutcome(__instance, __state, __exception);
    }
}
