using Common;
using HarmonyLib;
using GameInterface.Policies;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

[HarmonyPatch(typeof(JournalLogEntry), nameof(JournalLogEntry.IsEnded))]
internal class RevenueFarmingHistoryPatch
{
    private static void Postfix(JournalLogEntry __instance, ref bool __result)
    {
        // Rejoin loads every player's history from the server's campaign save.
        if (__result && ModInformation.IsClient && !CallOriginalPolicy.IsOriginalAllowed() &&
            __instance.Title.GetID() == "zqrn2beP" &&
            ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions))
            __result = sessions.IsVisible(__instance);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel))]
internal class RevenueFarmingCancellationLogPatch
{
    private static void Prefix(QuestBase __instance, TextObject cancelLog)
    {
        // Vanilla's all-raided cancellation leaves QUEST_GIVER unbound.
        if (__instance is Quest quest && cancelLog != null)
            StringHelpers.SetCharacterProperties("QUEST_GIVER", quest.QuestGiver.CharacterObject, cancelLog);
    }
}

[HarmonyPatch]
internal class RevenueFarmingDetachedDialoguePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithQuest));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithFail));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithBetrayal));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithTimedOut));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed));
    }

    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer ||
            !ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) ||
            sessions.Get(__instance) is not { } session ||
            Campaign.Current.QuestManager.Quests.Contains(session.Quest)) return;
        Campaign.Current.ConversationManager.RemoveRelatedLines(session.Quest);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.RemoveTrackedObject))]
internal class RevenueFarmingRemoveTrackingPatch
{
    // The server never starts its quest, and an owner's replay can precede StartQuest.
    private static bool Prefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
        => __instance is not Quest || __instance.IsTracked(trackedObject);
}

[HarmonyPatch(typeof(Quest), nameof(Quest.OnFinalize))]
internal class RevenueFarmingFinalizationMenuPatch
{
    private static bool Prefix(Quest __instance) => ModInformation.IsClient &&
        ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) &&
        sessions.IsLocal(__instance);
}

[HarmonyPatch(typeof(GauntletQueryManager), nameof(GauntletQueryManager.OnLateTick))]
internal class RevenueFarmingPaymentInquiryCleanupPatch
{
    private static void Prefix(object ____activeQueryData, ref Queue<Tuple<Type, object, bool, bool>> ____inquiryQueue)
    {
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions)) return;
        if (____inquiryQueue.Any(item => IsClosedPayment(item.Item2, sessions)))
            ____inquiryQueue = new Queue<Tuple<Type, object, bool, bool>>(
                ____inquiryQueue.Where(item => !IsClosedPayment(item.Item2, sessions)));
        if (IsClosedPayment(____activeQueryData, sessions)) InformationManager.HideInquiry();
    }

    private static bool IsClosedPayment(object data, IRevenueFarmingSessionStore sessions) =>
        data is InquiryData inquiry && inquiry.AffirmativeAction?.Target is Quest quest &&
        (sessions.LocalQuest != quest || sessions.Get(quest)?.AwaitingPayment != true);
}
