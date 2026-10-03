using Common;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;
using Common.Messaging;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.DoOptionContinue))]
internal sealed class ArtisanProductAcceptanceDialogPatch
{
    [HarmonyPrefix]
    private static bool WaitForAcceptance(ConversationManager __instance)
        => ModInformation.IsServer ||
            Hero.OneToOneConversationHero?.Issue is not ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue issue ||
            !issue.IsOngoingWithoutQuest || (__instance.ActiveToken != __instance.GetStateIndex("issue_classic_quest_start") &&
            __instance.ActiveToken != __instance.GetStateIndex("issue_offer_player_accept_lord_2"));
}

[HarmonyPatch(typeof(Quest))]
internal sealed class ArtisanProductQuestPresentationPatches
{
    [HarmonyPatch("BeforeGameMenuOpened")]
    [HarmonyPrefix]
    private static bool BeforeGameMenuOpened(Quest __instance, out bool __state)
    {
        __state = __instance._counterOfferGiven;
        return IsLocalOwner(__instance);
    }

    [HarmonyPatch("BeforeGameMenuOpened")]
    [HarmonyPostfix]
    private static void AfterGameMenuOpened(Quest __instance, bool __state)
    {
        if (__state || !__instance._counterOfferGiven || !IsLocalOwner(__instance)) return;
        MessageBroker.Instance.Publish(__instance, new ArtisanProductQuestActionRequested(
            __instance.QuestGiver, ArtisanProductQuestAction.MerchantOfferShown, __instance._deliveredRawGoods));
    }

    [HarmonyPatch("InitializeQuestOnGameLoad")]
    [HarmonyPrefix]
    private static bool InitializeQuestOnGameLoad(Quest __instance)
    {
        if (IsLocalOwner(__instance)) return true;
        Campaign.Current.ConversationManager.RemoveRelatedLines(__instance);
        __instance.OfferDialogFlow = null;
        __instance.DiscussDialogFlow = null;
        if (ModInformation.IsClient) Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(__instance);
        return false;
    }

    private static bool IsLocalOwner(Quest quest)
        => ModInformation.IsClient && ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(quest.QuestGiver);
}

[HarmonyPatch(typeof(ChangePlayerCharacterAction), nameof(ChangePlayerCharacterAction.Apply))]
internal sealed class ArtisanProductPlayerPresentationPatch
{
    [HarmonyPostfix]
    private static void RefreshOwnedQuests()
    {
        if (ModInformation.IsServer || Campaign.Current?.QuestManager == null) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>())
        {
            Campaign.Current.ConversationManager.RemoveRelatedLines(quest);
            Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
            if (!quest.IsOngoing || !ownership.IsLocalPeerOwner(quest.QuestGiver)) continue;
            quest.InitializeQuestOnGameLoad();
            quest.AddDialogs();
            quest.AddTrackedObject(quest.QuestGiver);
            quest.AddTrackedObject(quest._targetSettlement);
            quest.AddTrackedObject(quest._targetHero);
        }
        if (GangLeaderNeedsToOffloadStolenGoodsQuestType.OwnerTraitXpProgress.TryGet(Hero.MainHero, out var progress))
            Campaign.Current.PlayerTraitDeveloper = progress;
    }
}

[HarmonyPatch(typeof(QuestsVM))]
internal sealed class ArtisanProductJournalPresentationPatches
{
    [HarmonyPatch(MethodType.Constructor, new[] { typeof(System.Action) })]
    [HarmonyPostfix]
    private static void FilterJournal(QuestsVM __instance)
    {
        if (ModInformation.IsServer) return;
        foreach (var item in __instance.ActiveQuestsList.Where(item => !IsVisible(item)).ToArray())
            __instance.ActiveQuestsList.Remove(item);
        foreach (var item in __instance.OldQuestsList.Where(item => !IsVisible(item)).ToArray())
            __instance.OldQuestsList.Remove(item);
        var first = __instance.ActiveQuestsList.FirstOrDefault() ?? __instance.OldQuestsList.FirstOrDefault();
        if (__instance.SelectedQuest == null && first != null) __instance.SetSelectedItem(first);
        __instance.IsThereAnyQuest = first != null;
        __instance.RefreshValues();
    }

    [HarmonyPatch("SetSelectedItem")]
    [HarmonyPrefix]
    private static bool SelectVisibleItem(QuestItemVM quest) => IsVisible(quest);

    private static bool IsVisible(QuestItemVM item)
    {
        if (ModInformation.IsServer || item == null) return true;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership))
            return item.Quest is not Quest &&
                item.Issue is not ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
        if (item.Quest is Quest) return ownership.IsLocalPeerOwner(item.Quest.QuestGiver);
        if (item.Issue is ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue)
            return ownership.IsLocalPeerOwner(item.Issue.IssueOwner);
        return IsVisibleHistory(item.QuestLogEntry);
    }

    internal static bool IsVisibleHistory(JournalLogEntry entry)
    {
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) ||
            !ownership.TryGetJournalOwner(entry, out var owner)) return true;
        return ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) && controller.ControllerId == owner;
    }
}

[HarmonyPatch]
internal sealed class ArtisanProductNotificationPatches
{
    [HarmonyPatch(typeof(ViewDataTrackerCampaignBehavior), nameof(ViewDataTrackerCampaignBehavior.UnExaminedQuestLogs), MethodType.Getter)]
    [HarmonyPostfix]
    private static void FilterUnread(ViewDataTrackerCampaignBehavior __instance, ref IReadOnlyList<JournalLog> __result)
    {
        if (ModInformation.IsServer) return;
        __result = __result.Where(log => !__instance._unExaminedQuestLogJournalEntries.TryGetValue(log, out var history) ||
            ArtisanProductJournalPresentationPatches.IsVisibleHistory(history)).ToArray();
    }

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnQuestStarted")]
    [HarmonyPrefix]
    private static bool QuestStarted(QuestBase quest) => OwnedQuestNotification(quest);

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnQuestCompleted")]
    [HarmonyPrefix]
    private static bool QuestCompleted(QuestBase quest) => OwnedQuestNotification(quest);

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnQuestLogAdded")]
    [HarmonyPrefix]
    private static bool QuestLogAdded(QuestBase quest) => OwnedQuestNotification(quest);

    private static bool OwnedQuestNotification(QuestBase quest)
    {
        if (quest is not Quest) return true;
        if (ModInformation.IsServer || !ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership)) return false;
        if (ownership.IsLocalPeerOwner(quest.QuestGiver)) return true;
        var history = Campaign.Current.GetCampaignBehavior<JournalLogsCampaignBehavior>()?.GetRelatedLog(quest);
        return ownership.TryGetJournalOwner(history, out _) && ArtisanProductJournalPresentationPatches.IsVisibleHistory(history);
    }

    [HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnIssueUpdated")]
    [HarmonyPrefix]
    private static bool OwnedIssueNotification(IssueBase issue)
    {
        if (issue is not ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue) return true;
        return ModInformation.IsClient && ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
            ownership.IsLocalPeerOwner(issue.IssueOwner);
    }
}
