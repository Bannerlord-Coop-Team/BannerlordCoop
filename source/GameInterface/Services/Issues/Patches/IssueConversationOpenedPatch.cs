using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
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
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(issueGiver.Issue) &&
            !GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(issueGiver.Issue)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueGiver, new IssueConversationOpenedLocally(issueGiver, controllerIdProvider?.ControllerId));
    }
}
