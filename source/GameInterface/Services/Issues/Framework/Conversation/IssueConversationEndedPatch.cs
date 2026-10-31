using GameInterface.Services.Issues.Framework.Interface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation;

using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Framework.Conversation;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal static class IssueConversationEndedPatch
{
    [HarmonyPrefix]
    private static void Prefix(out QuestBase __state)
    {
        __state = Hero.OneToOneConversationHero?.Issue?.IssueQuest;
    }

    [HarmonyPostfix]
    private static void Postfix(QuestBase __state)
    {
        if (ModInformation.IsClient && __state != null)
            MessageBroker.Instance.Publish(typeof(IssueConversationEndedPatch), new QuestAcceptanceFinished(__state));
        if (ContainerProvider.TryResolve<IIssueConversationGuard>(out var conversationGuard))
        {
            conversationGuard.OnConversationEnded();
        }
    }
}
