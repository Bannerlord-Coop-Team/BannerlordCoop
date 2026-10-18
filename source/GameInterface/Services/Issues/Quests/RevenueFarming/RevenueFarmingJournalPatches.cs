using Common;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

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
