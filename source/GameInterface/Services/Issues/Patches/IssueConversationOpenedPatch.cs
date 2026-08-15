using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.BeginConversation))]
internal class IssueConversationOpenedPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        var issueGiver = Hero.OneToOneConversationHero;
        if (issueGiver?.Issue == null) return;
        var descriptor = QuestTypeRegistry.Get(issueGiver.Issue);
        if (descriptor?.SupportsQuestSolutionAccept != true && descriptor?.SupportsAlternativeAccept != true) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueGiver, new IssueConversationOpenedLocally(issueGiver, controllerIdProvider?.ControllerId));
    }
}
