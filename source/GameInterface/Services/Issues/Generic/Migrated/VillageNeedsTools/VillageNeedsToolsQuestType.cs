using Common.Messaging;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;

using Issue = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

internal static class VillageNeedsToolsQuestType
{
    private static readonly FieldInfo ExchangeItemField = AccessTools.Field(typeof(Issue), "_exchangeItem");
    private static readonly FieldInfo NumberOfExchangeItemField = AccessTools.Field(typeof(Issue), "_numberOfExchangeItem");
    private static readonly FieldInfo NumberOfRequestedItemField = AccessTools.Field(typeof(Issue), "_numberOfRequestedItem");
    private static readonly FieldInfo PaymentField = AccessTools.Field(typeof(Issue), "_payment");

    private sealed class CreationCaptureStrategyImpl
        : ICreationCaptureStrategy<Issue, (ItemObject RequestedItem, ItemObject ExchangeItem, int NumberOfExchangeItem, int NumberOfRequestedItem, int Payment)>
    {
        public bool TryCaptureFields(
            Issue issue, out (ItemObject RequestedItem, ItemObject ExchangeItem, int NumberOfExchangeItem, int NumberOfRequestedItem, int Payment) fields)
        {
            fields = default;
            if (issue == null) return false;

            var requestedItem = issue._requestedItem;
            if (requestedItem == null) return false;

            fields = (requestedItem, issue._exchangeItem, issue._numberOfExchangeItem, issue._numberOfRequestedItem, issue._payment);
            return true;
        }

        public Issue ConstructReplicated(
            Hero owner, (ItemObject RequestedItem, ItemObject ExchangeItem, int NumberOfExchangeItem, int NumberOfRequestedItem, int Payment) fields)
        {
            // The public ctor independently re-derives the other 4 fields from per-client state
            // (IssueDifficultyMultiplier/live Hearth) - force these readonly fields to the server's rolls instead.
            var issue = new Issue(owner, fields.RequestedItem);

            ExchangeItemField.SetValue(issue, fields.ExchangeItem);
            NumberOfExchangeItemField.SetValue(issue, fields.NumberOfExchangeItem);
            NumberOfRequestedItemField.SetValue(issue, fields.NumberOfRequestedItem);
            PaymentField.SetValue(issue, fields.Payment);

            return issue;
        }
    }

    private static readonly ICreationCaptureStrategy<Issue, (ItemObject RequestedItem, ItemObject ExchangeItem, int NumberOfExchangeItem, int NumberOfRequestedItem, int Payment)>
        CreationCaptureStrategy = new CreationCaptureStrategyImpl();

    public static readonly CreationCaptureRunner<Issue, (ItemObject RequestedItem, ItemObject ExchangeItem, int NumberOfExchangeItem, int NumberOfRequestedItem, int Payment)>
        CreationCapture = new(CreationCaptureStrategy, IssueBase.IssueFrequency.VeryCommon);

    private static void OnGenuineCreation(Issue issue)
    {
        MessageBroker.Instance.Publish(issue.IssueOwner, new VillageIssueCreated(issue));
    }

    public static bool TryTriggerOwnedAlternativeSolutionCompletion(Hero owner)
    {
        if (owner?.Issue is not Issue issue) return false;
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(owner)) return false;

        // Seeds the reason IssueFinalizedPatches.Postfix picks up when IssueFinalized() cascades below.
        global::GameInterface.Services.Issues.Patches.IssueManagerQuestCompletedReasonCapture.PendingReasons[owner] = VillageIssueFinalizeReason.AlternativeSolutionSuccess;

        issue.CompleteIssueWithAlternativeSolution(); // genuine call - NOT under AllowedThread, this is the real trigger
        return true;
    }

    static VillageNeedsToolsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsTools")
            .WithCreationCapture(CreationCaptureStrategy)
            .WithCreationTrigger(OnGenuineCreation)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
