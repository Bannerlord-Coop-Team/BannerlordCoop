using Common;
using HarmonyLib;
using GameInterface.Services.Issues.Framework.Finalization;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace GameInterface.Services.Issues.Quests.RevenueFarming;

using Quest = RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

[HarmonyPatch(typeof(FinalizationCoordinator), "Replay")]
internal class RevenueFarmingDetachedDialoguePatch
{
    private static void Prefix(IssueBase issue)
    {
        if (ModInformation.IsClient && issue.IssueQuest is Quest quest &&
            !Campaign.Current.QuestManager.Quests.Contains(quest))
            Campaign.Current.ConversationManager.RemoveRelatedLines(quest);
    }
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.RemoveTrackedObject))]
internal class RevenueFarmingRemoveTrackingPatch
{
    // The server never starts its quest, and an owner's replay can precede StartQuest.
    private static bool Prefix(QuestBase __instance, ITrackableCampaignObject trackedObject)
        => __instance is not Quest || __instance.IsTracked(trackedObject);
}

[HarmonyPatch(typeof(Quest), "OnFinalize")]
internal class RevenueFarmingFinalizationMenuPatch
{
    private static bool Prefix(Quest __instance) => ModInformation.IsClient &&
        ContainerProvider.TryResolve<IRevenueFarmingSessionStore>(out var sessions) &&
        sessions.IsLocal(__instance);
}

[HarmonyPatch(typeof(GauntletQueryManager), "OnLateTick")]
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
