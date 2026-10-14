using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithQuest))]
internal class StartIssueWithQuestDispatchPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (ModInformation.IsServer)
        {
            return;
        }

        if (CallOriginalPolicy.IsOriginalAllowed())
        {
            return;
        }

        MessageBroker.Instance.Publish(__instance, new QuestSolutionAcceptTriggered(__instance));
    }
}
