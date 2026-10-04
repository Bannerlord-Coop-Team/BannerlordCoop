using GameInterface.Policies;
using GameInterface.Services.Issues.Interfaces;
using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.GetCounterOfferersIssue))]
internal class ArtisanLordDiscussionOwnerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref IssueBase __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context)) return true;
        __result = null;
        if (Hero.OneToOneConversationHero == null) return false;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values)
        {
            if (issue.CounterOfferHero != Hero.OneToOneConversationHero || !issue.IsSolvingWithLordSolution) continue;
            if (issue is Issue && !context.IsLocalOwner(issue.IssueOwner)) continue;
            __result = issue;
            break;
        }
        return false;
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_alternative_solution_discussion_condition))]
internal class ArtisanAlternativeDiscussionOwnerPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (IssuesCampaignBehavior.GetIssueOwnersIssue() is not Issue issue) return true;
        if (ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) && context.IsLocalOwner(issue.IssueOwner)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal class ArtisanLordDiscussionConditionPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_reject_condition");
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_player_reject_condition");
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_accepted_condition");
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_player_accept_condition");
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_2_condition");
    }

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || IssuesCampaignBehavior.GetCounterOfferersIssue() != null) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal class ArtisanLordDiscussionConsequencePatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_reject_consequence");
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_counter_offer_accepted_consequence");
    }

    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        IssuesCampaignBehavior.GetCounterOfferersIssue() != null;
}

[HarmonyPatch(typeof(Quest), nameof(Quest.SetDialogs))]
internal class ArtisanQuestDiscussionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Quest __instance, out IDisposable __state)
    {
        __state = null;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsServer) return false;
        return ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) &&
            context.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();

    [HarmonyPostfix]
    internal static void Postfix(Quest __instance)
    {
        if (__instance.DiscussDialogFlow == null) return;
        foreach (var line in __instance.DiscussDialogFlow.Lines)
        {
            if (line.InputToken != "quest_discuss") continue;
            var original = line.ConditionDelegate;
            line.ConditionDelegate = () =>
                (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
                    (ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) &&
                        context.IsLocalOwner(__instance.QuestGiver))) && (original?.Invoke() ?? true);
        }
    }
}

[HarmonyPatch]
internal class ArtisanJournalOwnerPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(JournalLogsCampaignBehavior), "CreateRelatedLog", new[] { typeof(IssueBase) });
        yield return AccessTools.Method(typeof(JournalLogsCampaignBehavior), "CreateRelatedLog", new[] { typeof(QuestBase) });
    }

    [HarmonyPostfix]
    private static void Postfix(object __0)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (!ContainerProvider.TryResolve<IArtisanJournalOwnership>(out var journal)) return;
        if (__0 is Issue issue) journal.Record(issue, issue.IssueOwner);
        if (__0 is Quest quest) journal.Record(quest, quest.QuestGiver);
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.SyncData))]
internal class ArtisanJournalPersistencePatch
{
    [HarmonyPostfix]
    private static void Postfix(IDataStore dataStore)
    {
        if (ContainerProvider.TryResolve<IArtisanJournalOwnership>(out var journal)) journal.SyncData(dataStore);
    }
}

[HarmonyPatch(typeof(QuestsVM), MethodType.Constructor, typeof(Action))]
internal class ArtisanJournalVisibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(QuestsVM __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) ||
            !ContainerProvider.TryResolve<IArtisanJournalOwnership>(out var journal)) return;
        foreach (var entry in __instance.ActiveQuestsList.ToArray())
        {
            var giver = entry.Quest is Quest quest ? quest.QuestGiver : (entry.Issue as Issue)?.IssueOwner;
            if (giver != null && !context.IsLocalOwner(giver)) __instance.ActiveQuestsList.Remove(entry);
        }
        foreach (var entry in __instance.OldQuestsList.ToArray())
            if (!journal.IsVisible(entry.QuestLogEntry)) __instance.OldQuestsList.Remove(entry);

        if (__instance.SelectedQuest != null && !__instance.ActiveQuestsList.Contains(__instance.SelectedQuest) &&
            !__instance.OldQuestsList.Contains(__instance.SelectedQuest))
        {
            __instance.SelectedQuest.IsSelected = false;
            __instance.SelectedQuest = null;
            __instance.CurrentQuestStages.Clear();
            __instance.CurrentQuestTitle = string.Empty;
            __instance.IsCurrentQuestGiverHeroHidden = true;
            __instance._viewDataTracker.SetQuestSelection(null);
            var first = __instance.ActiveQuestsList.FirstOrDefault() ?? __instance.OldQuestsList.FirstOrDefault();
            if (first != null) __instance.SetSelectedItem(first);
        }
        __instance.IsThereAnyQuest = __instance.ActiveQuestsList.Count + __instance.OldQuestsList.Count > 0;
    }
}

[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior))]
internal class ArtisanQuestNotificationPatch
{
    private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnQuestStarted");
        yield return AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnQuestCompleted");
        yield return AccessTools.Method(typeof(DefaultNotificationsCampaignBehavior), "OnQuestLogAdded");
    }

    [HarmonyPrefix]
    private static bool QuestNotification(QuestBase quest) => quest is not Quest ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) && context.IsLocalOwner(quest.QuestGiver));

}

[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnIssueUpdated")]
internal class ArtisanIssueNotificationPatch
{
    [HarmonyPrefix]
    private static bool IssueNotification(IssueBase issue) => issue is not Issue ||
        CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() ||
        (ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) && context.IsLocalOwner(issue.IssueOwner));
}
