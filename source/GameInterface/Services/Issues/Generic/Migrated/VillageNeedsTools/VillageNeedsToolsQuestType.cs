using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;

using Issue = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct VillageNeedsToolsAcceptFields
{
    [ProtoMember(1)]
    public readonly int NumberOfRequestedGood;
    [ProtoMember(2)]
    public readonly int RewardGold;

    public VillageNeedsToolsAcceptFields(int numberOfRequestedGood, int rewardGold)
    {
        NumberOfRequestedGood = numberOfRequestedGood;
        RewardGold = rewardGold;
    }
}

[QuestTypeModule]
internal static class VillageNeedsToolsQuestType
{
    private static readonly FieldInfo ExchangeItemField = AccessTools.Field(typeof(Issue), "_exchangeItem");
    private static readonly FieldInfo NumberOfExchangeItemField = AccessTools.Field(typeof(Issue), "_numberOfExchangeItem");
    private static readonly FieldInfo NumberOfRequestedItemField = AccessTools.Field(typeof(Issue), "_numberOfRequestedItem");
    private static readonly FieldInfo PaymentField = AccessTools.Field(typeof(Issue), "_payment");

    private static readonly FieldInfo NumberOfRequestedGoodField = AccessTools.Field(typeof(Quest), "_numberOfRequestedGood");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

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
        return AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion(owner, RequestServerCompletion);
    }

    private static void RequestServerCompletion(Hero owner)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new RequestAlternativeSolutionCompletion(ownerId));
    }

    private static void RejectAcceptanceCore(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;
        if (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) &&
            ownershipRegistry.TryGetOwnerControllerId(owner, out _)) return;

        using (new IssueFinalizeAuthorityGuard())
        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }

    private sealed class QuestSolutionAcceptMirrorStrategy : IRaceArbitratedAcceptMirrorStrategy<VillageNeedsToolsAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue || !owner.Issue.IsOngoingWithoutQuest) return;

            using (new QuestSolutionStartAuthorityGuard())
            using (new Generic.Dispatch.IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out VillageNeedsToolsAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest) return false;

            fields = new VillageNeedsToolsAcceptFields(quest._numberOfRequestedGood, quest.RewardGold);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, VillageNeedsToolsAcceptFields fields)
        {
            if (owner?.Issue is not Issue) return;

            using (new QuestSolutionStartAuthorityGuard())
            using (new AllowedThread())
            {
                if (owner.Issue.IsOngoingWithoutQuest)
                {
                    Campaign.Current.IssueManager.StartIssueQuest(owner);
                }

                if (owner.Issue.IssueQuest is not Quest quest) return;

                NumberOfRequestedGoodField.SetValue(quest, fields.NumberOfRequestedGood);
                RewardGoldField.SetValue(quest, fields.RewardGold);
            }
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private static readonly IRaceArbitratedAcceptMirrorStrategy<VillageNeedsToolsAcceptFields> QuestSolutionAcceptMirror =
        new QuestSolutionAcceptMirrorStrategy();

    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedTradeGood) >= quest._numberOfRequestedGood;
    }

    private static bool ValidateQuestCancel(Issue issue)
    {
        var settlement = issue.IssueOwner.CurrentSettlement;
        if (settlement == null) return false;
        if (settlement.IsRaided || settlement.IsUnderRaid) return true;

        if (!TryResolveTrueOwnerHero(issue.IssueOwner, out var trueOwnerHero)) return false;

        var settlementFaction = settlement.MapFaction;
        var heroFaction = trueOwnerHero.MapFaction;
        return settlementFaction != null && heroFaction != null && settlementFaction.IsAtWarWith(heroFaction);
    }

    private static bool TryResolveTrueOwnerHero(Hero issueOwnerHero, out Hero trueOwnerHero)
    {
        trueOwnerHero = null;

        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return false;
        if (!ownershipRegistry.TryGetOwnerControllerId(issueOwnerHero, out var controllerId)) return false;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager)) return false;
        if (!playerManager.TryGetPlayer(controllerId, out var player) || player.HeroId == null) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        return objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out trueOwnerHero);
    }

    private static void ApplyQuestSuccessConsequence(Quest quest)
    {
        quest.AddLog(quest.QuestSuccessLog);
        quest.QuestGiver.AddPower(10f);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Honor, 30)
        });
        PartyBase.MainParty.ItemRoster.AddToCounts(quest._requestedTradeGood, -quest._numberOfRequestedGood);
        Quest.GiveTradeOrExchangeRewardToMainParty(quest.QuestGiver, quest.RewardGold, quest._exchangeItem, quest._numberOfExchangeItem);

        float hearthChange;
        if (quest._exchangeItem != null)
        {
            ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, 7);
            foreach (var notable in quest.QuestGiver.CurrentSettlement.Notables)
            {
                if (notable != quest.QuestGiver) ChangeRelationAction.ApplyPlayerRelation(notable, 2);
            }
            hearthChange = 40f;
        }
        else
        {
            ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, 5);
            hearthChange = 20f;
        }
        quest.QuestGiver.CurrentSettlement.Village.Hearth += hearthChange;
        quest.CompleteQuestWithSuccess();
    }

    static VillageNeedsToolsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsTools")
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept()
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessConsequence(ApplyQuestSuccessConsequence)
            .WithQuestCancelValidation(ValidateQuestCancel)
            .WithQuestFailValidation(issue => true)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
