using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(ConversationManager))]
internal class IssueConversationOpenedPatch
{
    // Map conversations, such as a settlement quick talk, start through SetupAndStartMapConversation and never call BeginConversation.
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.BeginConversation));
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.SetupAndStartMapConversation));
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        var issueGiver = Hero.OneToOneConversationHero;
        if (issueGiver?.Issue == null) return;
        if (!ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) ||
            !registry.TryGet(issueGiver.Issue.GetType(), out var descriptor) ||
            (descriptor.QuestSolutionAcceptStrategy == null && descriptor.AlternativeSolutionAcceptStrategy == null)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueGiver, new IssueConversationOpenedLocally(issueGiver, controllerIdProvider?.ControllerId));
    }
}
