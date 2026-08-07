using Common.Messaging;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;

using Issue = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;
using Quest = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

internal static class VillageNeedsCraftingMaterialsQuestType
{
    private static readonly FieldInfo RequestedItemField = AccessTools.Field(typeof(Issue), "_requestedItem");
    private static readonly FieldInfo RequestedItemAmountField = AccessTools.Field(typeof(Quest), "_requestedItemAmount");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));
    private static readonly FieldInfo PlayerAcceptedQuestLogField = AccessTools.Field(typeof(Quest), "_playerAcceptedQuestLog");
    private static readonly FieldInfo JournalLogRangeField = AccessTools.Field(typeof(JournalLog), nameof(JournalLog.Range));

    private static readonly Type IssueStateEnumType = AccessTools.Inner(typeof(IssueBase), "IssueState");
    private static readonly FieldInfo IssueStateField = AccessTools.Field(typeof(IssueBase), "_issueState");
    private static readonly object SolvingWithAlternativeSolutionStateValue =
        Enum.Parse(IssueStateEnumType, "SolvingWithAlternativeSolution");
    private static readonly PropertyInfo IsTriedToSolveBeforeProperty =
        AccessTools.Property(typeof(IssueBase), nameof(IssueBase.IsTriedToSolveBefore));

    private static void RejectAcceptanceCore(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;
        if (IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out _)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }

    private sealed class QuestSolutionAcceptMirrorStrategy : IRaceArbitratedAcceptMirrorStrategy<(int RequestedItemAmount, int RewardGold)>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue || !owner.Issue.IsOngoingWithoutQuest) return;

            using (new Generic.Dispatch.IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out (int RequestedItemAmount, int RewardGold) fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest) return false;

            fields = (quest._requestedItemAmount, quest.RewardGold);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, (int RequestedItemAmount, int RewardGold) fields)
        {
            if (owner?.Issue is not Issue) return;

            using (new AllowedThread())
            {
                if (owner.Issue.IsOngoingWithoutQuest)
                {
                    Campaign.Current.IssueManager.StartIssueQuest(owner);
                }

                if (owner.Issue.IssueQuest is not Quest quest) return;

                RequestedItemAmountField.SetValue(quest, fields.RequestedItemAmount);
                RewardGoldField.SetValue(quest, fields.RewardGold);

                if (PlayerAcceptedQuestLogField.GetValue(quest) is JournalLog log)
                {
                    JournalLogRangeField.SetValue(log, fields.RequestedItemAmount);
                }
            }
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private sealed class AlternativeAcceptMirrorStrategy : IAlternativeAcceptMirrorStrategy<AlternativeSolutionVanillaState>
    {
        public void MirrorAlternativeAccepted(Hero owner, AlternativeSolutionVanillaState state)
        {
            if (owner?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;

            using (new AllowedThread())
            {
                IssueStateField.SetValue(issue, SolvingWithAlternativeSolutionStateValue);
                IsTriedToSolveBeforeProperty.SetValue(issue, true);
                AlternativeSolutionVanillaStateSync.Apply(issue, state);
            }
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private static readonly ICreationCaptureStrategy<Issue, ItemObject> CreationCaptureStrategy =
        new FieldForceCreationCapture<Issue, ItemObject>(RequestedItemField, owner => new Issue(owner));

    private static readonly IRaceArbitratedAcceptMirrorStrategy<(int RequestedItemAmount, int RewardGold)> QuestSolutionAcceptMirror =
        new QuestSolutionAcceptMirrorStrategy();

    private static readonly IAlternativeAcceptMirrorStrategy<AlternativeSolutionVanillaState> AlternativeAcceptMirror =
        new AlternativeAcceptMirrorStrategy();

    public static readonly CreationCaptureRunner<Issue, ItemObject> CreationCapture = new(CreationCaptureStrategy);

    public static readonly RaceArbitratedAcceptMirrorHandler<(int RequestedItemAmount, int RewardGold)> QuestSolutionAccept =
        new(QuestSolutionAcceptMirror);

    public static readonly AlternativeAcceptMirrorHandler<AlternativeSolutionVanillaState> AlternativeAccept = new(AlternativeAcceptMirror);

    private static void OnGenuineCreation(Issue issue)
    {
        MessageBroker.Instance.Publish(issue.IssueOwner, new VillageCraftingIssueCreated(issue));
    }

    private static void OnGenuineQuestSolutionAccept(Hero issueOwner, string controllerId)
    {
        if (!QuestSolutionAccept.TryCaptureQuestFields(issueOwner, out var fields)) return;

        MessageBroker.Instance.Publish(issueOwner,
            new VillageCraftingIssueQuestAcceptTriggered(issueOwner, controllerId, fields.RequestedItemAmount, fields.RewardGold));
    }

    private static void OnGenuineAlternativeAccept(Hero issueOwner, string controllerId)
    {
        if (issueOwner?.Issue is not Issue issue) return;

        var state = AlternativeSolutionVanillaStateSync.Capture(issue);
        MessageBroker.Instance.Publish(issue, new VillageCraftingIssueAlternativeAcceptTriggered(issueOwner, controllerId, state));
    }

    public static bool TryTriggerOwnedAlternativeSolutionCompletion(Hero owner) =>
        AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion(owner,
            o => MessageBroker.Instance.Publish(o, new VillageCraftingIssueAlternativeSolutionCompletionRequested(o)));

    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedItem) >= quest._requestedItemAmount;
    }

    static VillageNeedsCraftingMaterialsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsCraftingMaterials")
            .WithCreationCapture(CreationCaptureStrategy)
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept(AlternativeAcceptMirror)
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestSolutionAcceptTrigger(OnGenuineQuestSolutionAccept)
            .WithAlternativeAcceptTrigger(OnGenuineAlternativeAccept)
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
