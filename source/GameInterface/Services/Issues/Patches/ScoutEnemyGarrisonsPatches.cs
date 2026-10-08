using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using Helpers;
using SandBox.CampaignBehaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue;
using Quest = ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsQuest;

[HarmonyPatch(typeof(ScoutEnemyGarrisonsIssueBehavior), nameof(ScoutEnemyGarrisonsIssueBehavior.OnCheckForIssue))]
internal static class ScoutEnemyGarrisonsGenerationPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(ScoutEnemyGarrisonsIssueBehavior), "SuitableSettlementCondition")]
internal static class ScoutEnemyGarrisonsTargetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Settlement settlement, Hero issueGiver, ref bool __result)
    {
        if (ModInformation.IsClient) return true;
        // Eligible players belong to the giver's faction; the dedicated server has no player faction.
        __result = settlement.IsFortification && settlement.MapFaction.IsAtWarWith(issueGiver.MapFaction) &&
            (!settlement.IsUnderSiege || settlement.SiegeEvent.BesiegerCamp.MapFaction != issueGiver.MapFaction);
        return false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.IssueStayAliveConditions))]
internal static class ScoutEnemyGarrisonsStayAlivePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (ModInformation.IsServer) return true;
        // Only the server can reroll target settlements or expire the offered issue.
        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(Issue), "TryToUpdateSettlements")]
internal static class ScoutEnemyGarrisonsRefreshTargetsPatch
{
    [HarmonyPostfix]
    private static void Postfix(Issue __instance, bool __result)
    {
        if (ModInformation.IsServer && __result)
            MessageBroker.Instance.Publish(__instance, new ScoutEnemyGarrisonsIssueChanged(__instance, false));
    }
}

[HarmonyPatch(typeof(IssueBase), "get_IssueQuestCanBeDuplicated")]
internal static class ScoutEnemyGarrisonsDuplicatePatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance, ref bool __result)
    {
        if (__instance is Issue && ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service))
            __result = !service.HasPersonalQuest(Hero.MainHero);
    }
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsAuthorityPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "HourlyTick", "OnSettlementOwnerChanged", "OnArmyDispersed", "OnClanChangedKingdom", "AllScoutingDone" })
            yield return AccessTools.DeclaredMethod(typeof(Quest), method);
    }

    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (ModInformation.IsClient || !__instance.IsOngoing) return false;
        if (!ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) return false;
        // Hourly cancellation checks diplomacy before reading the owner's position.
        var requireCurrentParty = __originalMethod.Name == "HourlyTick" &&
            new[] { __instance._questSettlement1, __instance._questSettlement2, __instance._questSettlement3 }
                .Any(target => target.Settlement.MapFaction.IsAtWarWith(__instance.QuestGiver.MapFaction));
        __state = service.OpenAuthority(__instance, requireCurrentParty);
        return __state != null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Quest __instance, IDisposable __state, Exception __exception)
    {
        try
        {
            if (__state != null && __exception == null && __instance.IsOngoing &&
                ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) service.PublishProgress(__instance);
        }
        finally
        {
            __state?.Dispose();
        }
    }
}

[HarmonyPatch(typeof(Quest), "QuestAcceptedConsequences")]
internal static class ScoutEnemyGarrisonsAcceptedPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(Quest __instance) => __instance._startQuestLog == null &&
        QuestSolutionStartAuthorityGuard.IsActive;
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
internal static class ScoutEnemyGarrisonsStartPatch
{
    [ThreadStatic] internal static bool SuppressConversationEnd;

    [HarmonyPrefix]
    internal static void Prefix(QuestBase __instance, out bool __state)
    {
        __state = SuppressConversationEnd;
        if (__instance is not Quest) return;
        SuppressConversationEnd = true;
        if (ModInformation.IsClient && ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) &&
            state.IsVisible(__instance) && state.PendingAcceptance?.IssueQuest == __instance)
        {
            var conversation = Campaign.Current.ConversationManager;
            SuppressConversationEnd = !conversation.IsConversationInProgress ||
                conversation.OneToOneConversationHero != __instance.QuestGiver ||
                conversation.ActiveToken != conversation.GetStateIndex("issue_classic_quest_start");
        }
    }

    [HarmonyFinalizer]
    internal static void Finalizer(bool __state) => SuppressConversationEnd = __state;
}

[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.OnConversationEnd))]
internal static class ScoutEnemyGarrisonsEncounterPatch
{
    [HarmonyPrefix]
    internal static bool Prefix() => !ScoutEnemyGarrisonsStartPatch.SuppressConversationEnd;
}

[HarmonyPatch(typeof(Quest), "OnTimedOut")]
internal static class ScoutEnemyGarrisonsTimeoutLogPatch
{
    [HarmonyPrefix]
    private static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsCompletionPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { nameof(QuestBase.CompleteQuestWithSuccess), nameof(QuestBase.CompleteQuestWithCancel),
            nameof(QuestBase.CompleteQuestWithTimeOut), nameof(QuestBase.CompleteQuestWithFail), nameof(QuestBase.CompleteQuestWithBetrayal) })
            yield return AccessTools.DeclaredMethod(typeof(QuestBase), method);
    }

    [HarmonyPrefix]
    internal static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest quest) return true;
        if (!quest.IsOngoing) return false;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive && AllowedThread.IsThisThreadAllowed();
        if (!ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state)) return false;
        if (state.TryGet(quest, out var owner) && ScoutEnemyGarrisonsPlayerChangePatch.ChangingPlayer != null &&
            owner.Hero != ScoutEnemyGarrisonsPlayerChangePatch.ChangingPlayer) return false;
        if (!ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) return false;
        __state = service.OpenAuthority(quest, requireCurrentParty: false);
        if (__state != null) return true;
        if (__originalMethod.Name != nameof(QuestBase.CompleteQuestWithCancel) || quest.QuestGiver?.Issue?.IssueQuest == quest) return false;
        __state = new IssueFinalizeAuthorityGuard();
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsIssueCompletionPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { nameof(IssueBase.CompleteIssueWithCancel), nameof(IssueBase.CompleteIssueWithFail),
            nameof(IssueBase.CompleteIssueWithBetrayal), nameof(IssueBase.CompleteIssueWithAiLord) })
            yield return AccessTools.DeclaredMethod(typeof(IssueBase), method);
    }

    [HarmonyPrefix]
    internal static bool Prefix(IssueBase __instance, MethodBase __originalMethod, object[] __args,
        out IssueFinalizeAuthorityGuard __state)
    {
        __state = null;
        if (__instance is not Issue) return true;
        if (ModInformation.IsClient && !(IssueFinalizeAuthorityGuard.IsActive && AllowedThread.IsThisThreadAllowed())) return false;
        __state = new IssueFinalizeAuthorityGuard();
        if (__instance.IssueQuest is not Quest quest || !quest.IsOngoing) return true;

        // Quest completion dispatches the issue completion after finalizing the quest.
        var log = __args.Length == 0 ? null : (TextObject)__args[0];
        switch (__originalMethod.Name)
        {
            case nameof(IssueBase.CompleteIssueWithCancel): quest.CompleteQuestWithCancel(log); break;
            case nameof(IssueBase.CompleteIssueWithFail): quest.CompleteQuestWithFail(log); break;
            case nameof(IssueBase.CompleteIssueWithBetrayal): quest.CompleteQuestWithBetrayal(); break;
        }
        return false;
    }

    [HarmonyFinalizer]
    private static void Finalizer(IssueFinalizeAuthorityGuard __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestBase), "FinalizeQuest")]
internal static class ScoutEnemyGarrisonsFinalProgressPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance)
    {
        // The journal and final counters must arrive before the generic issue removal.
        if (ModInformation.IsServer && __instance is Quest quest &&
            ContainerProvider.TryResolve<IScoutEnemyGarrisonsService>(out var service)) service.PublishProgress(quest);
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal static class ScoutEnemyGarrisonsPlayerChangePatch
{
    [ThreadStatic] internal static Hero ChangingPlayer;

    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, out Hero __state)
    {
        __state = ChangingPlayer;
        ChangingPlayer = oldPlayer;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Hero __state) => ChangingPlayer = __state;
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), "OnIssueUpdated")]
internal static class ScoutEnemyGarrisonsRewardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue) => issue is not Issue || ModInformation.IsServer;
}

[HarmonyPatch(typeof(Quest), "InitializeQuestOnGameLoad")]
internal static class ScoutEnemyGarrisonsRestorePatch
{
    [HarmonyPrefix]
    private static void Prefix(Quest __instance)
    {
        if (ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state)) state.Restore(__instance);
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal static class ScoutEnemyGarrisonsSavePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (!ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state)) return;
        if (dataStore.IsSaving)
        {
            foreach (var quest in Campaign.Current.QuestManager._quests.OfType<Quest>()) state.Capture(quest);
        }
        state.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, new[] { typeof(Action) })]
internal static class ScoutEnemyGarrisonsJournalViewPatch
{
    [ThreadStatic] internal static bool IsOpening;

    [HarmonyPrefix]
    private static void Prefix(out bool __state)
    {
        __state = IsOpening;
        IsOpening = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => IsOpening = __state;
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.Quests), MethodType.Getter)]
internal static class ScoutEnemyGarrisonsQuestListPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref MBReadOnlyList<QuestBase> __result)
    {
        if (ModInformation.IsClient && ScoutEnemyGarrisonsJournalViewPatch.IsOpening &&
            ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state))
            __result = new MBList<QuestBase>(__result.Where(state.IsVisible));
    }
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.AddTrackedObjectForQuest))]
internal static class ScoutEnemyGarrisonsTrackedAddPatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestBase relatedQuest) => relatedQuest is not Quest || ModInformation.IsServer ||
        (ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(relatedQuest));
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.RemoveTrackedObjectForQuest))]
internal static class ScoutEnemyGarrisonsTrackedRemovePatch
{
    [HarmonyPrefix]
    private static bool Prefix(QuestManager __instance, ITrackableCampaignObject trackedObject, QuestBase relatedQuest)
    {
        if (relatedQuest is not Quest || trackedObject is not Settlement) return true;
        if (__instance._trackedObjects.TryGetValue(trackedObject, out var quests) && quests.Remove(relatedQuest))
        {
            if (quests.Count == 0) __instance._trackedObjects.Remove(trackedObject);
            if (relatedQuest.IsTrackEnabled) Campaign.Current.VisualTrackerManager.RemoveTrackedObject(trackedObject);
        }
        return false;
    }
}

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.DoOption), new[] { typeof(int) })]
internal static class ScoutEnemyGarrisonsAcceptOptionPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(ConversationManager __instance, int optionIndex)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state)) return true;
        if (state.PendingAcceptance != null) return false;
        if (__instance.CurOptions[optionIndex].Id == "issue_offer_player_accept_quest" &&
            __instance.OneToOneConversationHero?.Issue is Issue issue) state.PendingAcceptance = issue;
        return true;
    }
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsAcceptContinuePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.DoOptionContinue));
        yield return AccessTools.DeclaredMethod(typeof(ConversationManager), nameof(ConversationManager.ContinueConversation));
    }

    [HarmonyPrefix]
    internal static bool Prefix() => ModInformation.IsServer ||
        !ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) || state.PendingAcceptance == null;
}

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal static class ScoutEnemyGarrisonsAcceptEndPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state)) state.PendingAcceptance = null;
    }
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsPresentationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "AddDialogs", nameof(QuestBase.AddTrackedObject), nameof(QuestBase.ToggleTrackedObjects) })
            yield return AccessTools.DeclaredMethod(typeof(QuestBase), method);
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod)
    {
        if (__instance is not Quest) return true;
        if (ModInformation.IsServer) return __originalMethod.Name != "AddDialogs";
        return ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(__instance);
    }
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsJournalPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "OnQuestStarted", "OnQuestCompleted", "OnQuestLogAdded" })
            yield return AccessTools.DeclaredMethod(typeof(JournalLogsCampaignBehavior), method);
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase quest) => quest is not Quest || ModInformation.IsServer ||
        (ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(quest));
}

[HarmonyPatch]
internal static class ScoutEnemyGarrisonsNotificationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in new[] { "OnQuestStarted", "OnQuestCompleted", "OnQuestLogAdded" })
            yield return AccessTools.DeclaredMethod(typeof(DefaultNotificationsCampaignBehavior), method);
    }

    [HarmonyPrefix]
    internal static bool Prefix(QuestBase quest) => quest is not Quest ||
        (ModInformation.IsClient && ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(quest));
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_task_given_on_condition")]
internal static class ScoutEnemyGarrisonsDiscussionPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (__result && Hero.OneToOneConversationHero?.Issue?.IssueQuest is Quest quest)
            __result = ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(quest);
    }
}

[HarmonyPatch(typeof(JournalLogsCampaignBehavior), "OnIssueUpdated")]
internal static class ScoutEnemyGarrisonsIssueJournalPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue) => issue is not Issue ||
        (issue.IssueQuest is Quest && (ModInformation.IsServer ||
            (ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && state.IsVisible(issue.IssueQuest))));
}

[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal static class ScoutEnemyGarrisonsHistoryPatch
{
    [HarmonyPostfix]
    private static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        if (ModInformation.IsClient && ScoutEnemyGarrisonsJournalViewPatch.IsOpening &&
            ContainerProvider.TryResolve<IScoutEnemyGarrisonsQuestState>(out var state) && !state.IsVisible(__instance))
            __result = false;
    }
}
