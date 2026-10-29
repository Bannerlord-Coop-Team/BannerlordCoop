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
        if (ContainerProvider.TryResolve<IIssueConversationGuard>(out var conversationGuard))
        {
            conversationGuard.OnConversationEnded();
        }
    }
}
