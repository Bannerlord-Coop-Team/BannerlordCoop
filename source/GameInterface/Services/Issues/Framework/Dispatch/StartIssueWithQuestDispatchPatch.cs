using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithQuest))]
internal class StartIssueWithQuestDispatchPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowed() &&
            !__instance.IsOngoingWithoutQuest && ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) &&
            registry.IsRegistered(__instance.GetType()) &&
            ContainerProvider.TryResolve<IIssueConversationGuard>(out var conversationGuard))
        {
            // No quest is made, so nothing would answer the accept line
            conversationGuard.EndUnavailableAccept(__instance.IssueOwner);
            // IssueManager treats false as a failed offer and finalizes the existing issue.
            __result = true;
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, bool __runOriginal)
    {
        if (ModInformation.IsServer || !__runOriginal)
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
