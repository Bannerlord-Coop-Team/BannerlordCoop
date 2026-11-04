using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation;

namespace GameInterface.Services.Issues.Framework.Conversation;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal static class IssueConversationEndedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (ModInformation.IsClient)
            MessageBroker.Instance.Publish(typeof(IssueConversationEndedPatch), new QuestAcceptanceFinished());

        if (ContainerProvider.TryResolve<IIssueConversationGuard>(out var conversationGuard))
        {
            conversationGuard.OnConversationEnded();
        }
    }
}
