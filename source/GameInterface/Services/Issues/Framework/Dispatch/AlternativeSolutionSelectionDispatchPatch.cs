using Common;
using Common.Util;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Issues.Framework.Dispatch;

[HarmonyPatchCategory(GameInterface.HARMONY_QUESTS_CATEGORY)]
internal static class AlternativeSolutionSelectionDispatchPatch
{
    private sealed class State : IDisposable
    {
        private readonly AllowedThread allowedThread;
        private readonly AlternativeSolutionSelectionScope selection;

        public State()
        {
            allowedThread = new AllowedThread();
            selection = new AlternativeSolutionSelectionScope();
        }

        public void Dispose()
        {
            selection.Dispose();
            allowedThread.Dispose();
        }
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        var method = AccessTools.DeclaredMethod(typeof(IssuesCampaignBehavior),
            "issue_offer_player_accept_alternative_3_consequence");
        if (method != null) yield return method;
    }

    [HarmonyPrefix]
    private static void Prefix(out State __state)
    {
        __state = null;
        if (ModInformation.IsServer || !IsRegisteredAlternativeConversation()) return;
        __state = new State();
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception exception, State __state)
    {
        __state?.Dispose();
        return exception;
    }

    private static bool IsRegisteredAlternativeConversation()
    {
        var giver = Hero.OneToOneConversationHero;
        return giver?.Issue != null &&
            ContainerProvider.TryResolve<IQuestTypeRegistry>(out var registry) &&
            registry.TryGet(giver.Issue.GetType(), out var descriptor) &&
            descriptor.AlternativeSolutionAcceptStrategy != null;
    }
}
