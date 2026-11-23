using Common;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;

namespace GameInterface.Services.Issues.Framework.Conversation;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
[HarmonyPatch(typeof(ConversationManager))]
internal static class IssueConversationOpenedPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.BeginConversation));
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.SetupAndStartMapConversation));
    }

    [HarmonyPostfix]
    private static void Postfix()
    {
        var giver = Hero.OneToOneConversationHero;
        if (giver == null || !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !objects.TryGetId(giver, out var giverId)) return;

        MessageBroker.Instance.Publish(typeof(IssueConversationOpenedPatch),
            new IssueConversationOpenedLocally(giverId));
    }
}
