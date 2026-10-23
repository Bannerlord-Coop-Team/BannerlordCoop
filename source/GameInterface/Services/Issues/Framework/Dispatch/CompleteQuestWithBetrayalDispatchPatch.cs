using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal))]
internal class CompleteQuestWithBetrayalDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, TextObject betrayLog, out IssueBase __state)
    {
        __state = null;
        if (!ContainerProvider.TryResolve<IOutcomeDispatcher>(out var dispatcher))
        {
            return true;
        }

        return dispatcher.BeforeQuestOutcome(__instance, IssueOutcome.QuestBetrayal, out __state, betrayLog);
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
