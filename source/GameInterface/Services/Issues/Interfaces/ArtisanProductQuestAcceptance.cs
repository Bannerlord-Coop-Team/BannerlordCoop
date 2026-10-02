using Common.Util;
using Common;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;
using Quest = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssueQuest;

internal interface IArtisanProductQuestAcceptance : IRaceArbitratedAcceptMirrorStrategy<ArtisanProductQuestAcceptFields>,
    IAlternativeAcceptMirrorStrategy<ArtisanProductAlternativeAcceptFields>
{
    bool HasConflictingIssue(Issue issue);
}

internal sealed class ArtisanProductQuestAcceptance : IArtisanProductQuestAcceptance
{
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IControllerIdProvider controller;
    private readonly IIssueOwnershipRegistry ownership;

    public ArtisanProductQuestAcceptance(IObjectManager objects, IPlayerManager players, IControllerIdProvider controller,
        IIssueOwnershipRegistry ownership)
    {
        this.objects = objects;
        this.players = players;
        this.controller = controller;
        this.ownership = ownership;
    }

    public bool HasConflictingIssue(Issue issue)
    {
        if (!TryGetController(out var controllerId)) return true;
        return HasConflictingIssue(Campaign.Current.IssueManager.Issues.Values, issue, controllerId);
    }

    internal bool HasConflictingIssue(IEnumerable<IssueBase> issues, Issue issue, string controllerId)
    {
        return issues.Any(candidate => candidate is Issue && candidate != issue &&
            (candidate.IsSolvingWithQuest || candidate.IsSolvingWithAlternative) &&
            ownership.TryGetOwnerControllerId(candidate.IssueOwner, out var owner) && owner == controllerId);
    }

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (!TryGetController(out _)) return;
        if (!issue.CheckPreconditions(owner, out _)) return;
        using (new IssueDispatchReplayGuard())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
            if (issue.IssueQuest is Quest quest && quest._playerStartsQuestLog == null)
                quest.QuestAcceptedConsequences();
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out ArtisanProductQuestAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || issue.IssueQuest is not Quest quest || !quest.IsOngoing) return false;
        if (!TryGetController(out var controllerId) || quest._playerStartsQuestLog == null) return false;
        fields = new ArtisanProductQuestAcceptFields(quest._amountOfRawGoodsToBeDelivered, quest.RewardGold,
            issue.IssueDifficultyMultiplier, quest.QuestDueTime, quest._playerStartsQuestLog.LogTime, controllerId);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, ArtisanProductQuestAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        if (fields.Amount <= 0 || fields.Reward < 0 || fields.Difficulty <= 0 || string.IsNullOrEmpty(fields.ControllerId))
            throw new InvalidOperationException("Invalid artisan acceptance fields");

        using (new AllowedThread())
        {
            issue._issueDifficultyMultiplier = fields.Difficulty;
            var quest = new Quest(issue.StringId + "_quest", owner, fields.DueTime, issue._targetSettlement,
                issue._rawMaterialsToBeDelivered, fields.Amount, fields.Reward, issue._targetHero, issue.CounterOfferHero);
            issue.IssueQuest = quest;
            issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
            issue.IssueDueTime = CampaignTime.Never;
            issue.IsTriedToSolveBefore = true;
            // Starting a received mirror must not grant goods or end an observer's conversation.
            quest._questState = QuestBase.QuestStates.Ongoing;
            ownership.SetOwner(owner, fields.ControllerId);
            Campaign.Current.QuestManager.OnQuestStarted(quest);
            var taskName = new TextObject("{=L700FNht}Delivered {RAW_MATERIAL}");
            taskName.SetTextVariable("RAW_MATERIAL", issue._rawMaterialsToBeDelivered.Name);
            quest._playerStartsQuestLog = new JournalLog(fields.StartedAt, quest.PlayerStartsQuestLogText,
                taskName, 0, fields.Amount, LogType.Discreate);
            quest._journalEntries.Add(quest._playerStartsQuestLog);
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
            if (controller.ControllerId == fields.ControllerId)
            {
                quest.RegisterEvents();
                quest.AddTrackedObject(owner);
                quest.AddTrackedObject(issue._targetSettlement);
                quest.AddTrackedObject(issue._targetHero);
                Campaign.Current.ConversationManager.AddDialogFlow(quest.GetCounterOfferDialogFlow(), quest);
                Campaign.Current.ConversationManager.AddDialogFlow(quest.GetDeliveryDialogFlow(), quest);
                ResumeAcceptanceDialog(owner, accepted: true);
            }
            else
            {
                Campaign.Current.ConversationManager.RemoveRelatedLines(quest);
            }
        }
    }

    public void ReplayAlternativeAccepted(Hero owner)
    {
        if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        using (new IssueDispatchReplayGuard())
            issue.StartIssueWithAlternativeSolution();
    }

    public bool TryCaptureAlternativeFields(Hero owner, out ArtisanProductAlternativeAcceptFields fields)
    {
        fields = default;
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return false;
        if (issue.JournalEntries.Count == 0) return false;
        fields = new ArtisanProductAlternativeAcceptFields(issue.IssueDifficultyMultiplier, issue.JournalEntries[0].LogTime);
        return true;
    }

    public void MirrorAlternativeAccepted(Hero owner, ArtisanProductAlternativeAcceptFields fields)
    {
        if (owner?.Issue is not Issue issue || !issue.IsSolvingWithAlternative) return;
        if (fields.Difficulty <= 0) throw new InvalidOperationException("Invalid artisan alternative difficulty");
        issue._issueDifficultyMultiplier = fields.Difficulty;
        if (issue._journalEntries.Count == 0) return;
        var previous = issue._journalEntries[0];
        issue._journalEntries[0] = new JournalLog(fields.StartedAt, issue.AlternativeSolutionStartLog,
            previous.TaskName, previous.CurrentProgress, issue.AlternativeSolutionBaseDurationInDaysInternal, previous.Type);
        Campaign.Current.GetCampaignBehavior<ViewDataTrackerCampaignBehavior>()?._unExaminedQuestLogs.Remove(previous);
        CampaignEventDispatcher.Instance.OnIssueLogAdded(issue, true);
    }

    public void RejectAcceptance(Hero owner)
    {
        // Selection was restored after sending; a rejection must not return another owner's accepted troops.
        ResumeAcceptanceDialog(owner, accepted: false);
    }

    internal static void ResumeAcceptanceDialog(Hero owner, bool accepted)
    {
        var conversation = Campaign.Current.ConversationManager;
        if (!conversation.IsConversationInProgress || Hero.OneToOneConversationHero != owner ||
            conversation.ActiveToken != conversation.GetStateIndex("issue_classic_quest_start")) return;
        if (!accepted) conversation.ActiveToken = conversation.GetStateIndex("issue_offer_hero_response_reject");
        if (conversation.IsConversationFlowActive) conversation.DoOptionContinue();
        else conversation._executeDoOptionContinue = true;
    }

    private bool TryGetController(out string controllerId)
    {
        controllerId = null;
        if (!objects.TryGetIdWithLogging(Hero.MainHero, out var heroId)) return false;
        controllerId = players.Players.FirstOrDefault(p => p.HeroId == heroId)?.ControllerId;
        return !string.IsNullOrEmpty(controllerId);
    }
}
