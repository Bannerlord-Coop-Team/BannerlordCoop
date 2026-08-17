using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods;

using Issue = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue;
using Quest = GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderStolenGoodsQuestAcceptFields
{
    [ProtoMember(1)]
    public readonly int StolenTradeGoodAmount;
    [ProtoMember(2)]
    public readonly int StolenTradeGoodPrice;
    [ProtoMember(3)]
    public readonly int RewardGold;
    [ProtoMember(4)]
    public readonly int CounterOfferGold;

    public GangLeaderStolenGoodsQuestAcceptFields(int stolenTradeGoodAmount, int stolenTradeGoodPrice, int rewardGold, int counterOfferGold)
    {
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        StolenTradeGoodPrice = stolenTradeGoodPrice;
        RewardGold = rewardGold;
        CounterOfferGold = counterOfferGold;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct GangLeaderStolenGoodsAlternativeAcceptPayload
{
    [ProtoMember(1)]
    public readonly int StolenTradeGoodAmount;
    [ProtoMember(2)]
    public readonly int RewardGold;
    [ProtoMember(3)]
    public readonly AlternativeSolutionVanillaState State;

    public GangLeaderStolenGoodsAlternativeAcceptPayload(int stolenTradeGoodAmount, int rewardGold, AlternativeSolutionVanillaState state)
    {
        StolenTradeGoodAmount = stolenTradeGoodAmount;
        RewardGold = rewardGold;
        State = state;
    }
}

[QuestTypeModule]
internal static class GangLeaderNeedsToOffloadStolenGoodsQuestType
{
    private static readonly FieldInfo RandomForStolenTradeGoodField =
        AccessTools.Field(typeof(Issue), "_randomForStolenTradeGood");
    private static readonly FieldInfo IssueHideoutField =
        AccessTools.Field(typeof(Issue), "_issueHideout");
    private static readonly PropertyInfo CounterOfferHeroProperty =
        AccessTools.Property(typeof(Issue), nameof(IssueBase.CounterOfferHero));

    private static readonly FieldInfo StolenTradeGoodAmountField = AccessTools.Field(typeof(Quest), "_stolenTradeGoodAmount");
    private static readonly FieldInfo StolenTradeGoodPriceField = AccessTools.Field(typeof(Quest), "_stolenTradeGoodPrice");
    private static readonly FieldInfo CounterOfferGoldField = AccessTools.Field(typeof(Quest), "_counterOfferGold");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    private static void RejectAcceptanceCore(Hero owner)
    {
        if (owner?.Issue == null || owner.Issue.IsOngoingWithoutQuest) return;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry)) return;
        if (ownershipRegistry.TryGetOwnerControllerId(owner, out _)) return;

        using (new AllowedThread())
        {
            owner.Issue.CompleteIssueWithCancel();
        }
    }

    private sealed class CreationCaptureStrategyImpl
        : ICreationCaptureStrategy<Issue, (Settlement IssueHideout, int RandomForStolenTradeGood, Hero CounterOfferHero)>
    {
        public bool TryCaptureFields(
            Issue issue, out (Settlement IssueHideout, int RandomForStolenTradeGood, Hero CounterOfferHero) fields)
        {
            fields = default;
            if (issue == null) return false;

            var issueHideout = (Settlement)IssueHideoutField.GetValue(issue);
            var randomForStolenTradeGood = (int)RandomForStolenTradeGoodField.GetValue(issue);
            var counterOfferHero = issue.CounterOfferHero;

            if (issueHideout == null || counterOfferHero == null) return false;

            fields = (issueHideout, randomForStolenTradeGood, counterOfferHero);
            return true;
        }

        public Issue ConstructReplicated(Hero owner, (Settlement IssueHideout, int RandomForStolenTradeGood, Hero CounterOfferHero) fields)
        {
            var issue = new Issue(owner, fields.IssueHideout);
            RandomForStolenTradeGoodField.SetValue(issue, fields.RandomForStolenTradeGood);
            return issue;
        }
    }

    private sealed class QuestSolutionAcceptMirrorStrategy : IRaceArbitratedAcceptMirrorStrategy<GangLeaderStolenGoodsQuestAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue || !owner.Issue.IsOngoingWithoutQuest) return;

            using (new Dispatch.IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out GangLeaderStolenGoodsQuestAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest) return false;

            fields = new GangLeaderStolenGoodsQuestAcceptFields(
                (int)StolenTradeGoodAmountField.GetValue(quest),
                (int)StolenTradeGoodPriceField.GetValue(quest),
                quest.RewardGold,
                (int)CounterOfferGoldField.GetValue(quest));
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, GangLeaderStolenGoodsQuestAcceptFields fields)
        {
            if (owner?.Issue is not Issue issue) return;

            using (new AllowedThread())
            {
                if (issue.IsOngoingWithoutQuest)
                {
                    issue._issueState = IssueBase.IssueState.SolvingWithQuestSolution;
                    issue.IsTriedToSolveBefore = true;
                }

                if (issue.IssueQuest is not Quest quest) return;

                StolenTradeGoodAmountField.SetValue(quest, fields.StolenTradeGoodAmount);
                StolenTradeGoodPriceField.SetValue(quest, fields.StolenTradeGoodPrice);
                RewardGoldField.SetValue(quest, fields.RewardGold);
                CounterOfferGoldField.SetValue(quest, fields.CounterOfferGold);
            }
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private sealed class AlternativeAcceptMirrorStrategy : IAlternativeAcceptMirrorStrategy<GangLeaderStolenGoodsAlternativeAcceptPayload>
    {
        public void ReplayAlternativeAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue || !owner.Issue.IsOngoingWithoutQuest) return;

            using (new Dispatch.IssueDispatchReplayGuard())
            {
                owner.Issue.StartIssueWithAlternativeSolution();
            }
        }

        public bool TryCaptureAlternativeFields(Hero owner, out GangLeaderStolenGoodsAlternativeAcceptPayload payload)
        {
            payload = default;
            if (owner?.Issue is not Issue issue) return false;

            payload = new GangLeaderStolenGoodsAlternativeAcceptPayload(
                issue.StolenTradeGoodAmount, issue.RewardGold, AlternativeSolutionVanillaStateSync.Capture(issue));
            AlternativeSolutionFreeze.Freeze(owner, (payload.StolenTradeGoodAmount, payload.RewardGold));
            return true;
        }

        public void MirrorAlternativeAccepted(Hero owner, GangLeaderStolenGoodsAlternativeAcceptPayload payload)
        {
            if (owner?.Issue is not Issue issue) return;

            if (issue.IsOngoingWithoutQuest)
            {
                using (new AllowedThread())
                {
                    issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                    issue.IsTriedToSolveBefore = true;
                    AlternativeSolutionVanillaStateSync.Apply(issue, payload.State);
                }
            }

            AlternativeSolutionFreeze.Freeze(owner, (payload.StolenTradeGoodAmount, payload.RewardGold));
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private static readonly ICreationCaptureStrategy<Issue, (Settlement IssueHideout, int RandomForStolenTradeGood, Hero CounterOfferHero)>
        CreationCaptureStrategy = new CreationCaptureStrategyImpl();

    private static readonly IRaceArbitratedAcceptMirrorStrategy<GangLeaderStolenGoodsQuestAcceptFields>
        QuestSolutionAcceptMirror = new QuestSolutionAcceptMirrorStrategy();

    private static readonly IAlternativeAcceptMirrorStrategy<GangLeaderStolenGoodsAlternativeAcceptPayload>
        AlternativeAcceptMirror = new AlternativeAcceptMirrorStrategy();

    public static readonly PendingRegistryPayloadFreeze<(int StolenTradeGoodAmount, int RewardGold)> AlternativeSolutionFreeze = new();

    public static readonly CreationCaptureRunner<Issue, (Settlement IssueHideout, int RandomForStolenTradeGood, Hero CounterOfferHero)>
        CreationCapture = new(CreationCaptureStrategy, IssueBase.IssueFrequency.Common);

    public static Issue ConstructAndRegisterReplicated(Hero owner, Settlement issueHideout, int randomForStolenTradeGood, Hero counterOfferHero) =>
        CreationCapture.ConstructAndRegisterReplicated(
            owner,
            (issueHideout, randomForStolenTradeGood, counterOfferHero),
            (issue, fields) => CounterOfferHeroProperty.SetValue(issue, fields.CounterOfferHero));

    private static void OnGenuineCreation(Issue issue)
    {
        MessageBroker.Instance.Publish(issue.IssueOwner, new GangLeaderStolenGoodsIssueCreated(issue));
    }

    public static bool TryTriggerOwnedAlternativeSolutionCompletion(Hero owner) =>
        AlternativeSolutionCompletionRunner.TryTriggerOwnedCompletion(owner, RequestServerCompletion);

    private static void RequestServerCompletion(Hero owner)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;
        if (!ContainerProvider.TryResolve<INetwork>(out var network)) return;
        if (!objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new RequestAlternativeSolutionCompletion(ownerId));
    }

    private static bool ValidateQuestCancel(Issue issue) => true;

    static GangLeaderNeedsToOffloadStolenGoodsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsToOffloadStolenGoods")
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept(AlternativeAcceptMirror)
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestCancelValidation(ValidateQuestCancel)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
