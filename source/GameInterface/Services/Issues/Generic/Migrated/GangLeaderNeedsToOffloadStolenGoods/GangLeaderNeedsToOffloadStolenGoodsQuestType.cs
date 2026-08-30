using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.LinQuick;

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

            var issueHideout = issue._issueHideout;
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
                    using (new Dispatch.IssueDispatchReplayGuard())
                    using (new QuestSolutionStartAuthorityGuard())
                    {
                        Campaign.Current.IssueManager.StartIssueQuest(owner);
                    }
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
    private static bool ValidateQuestFail(Issue issue) => true;
    private static bool ValidateQuestBetrayal(Issue issue) => QuestBetrayalProofContext.Current != 0;
    private static bool ValidateQuestSuccess(Issue issue, MobileParty party) => QuestSuccessProofContext.Current != 0;

    internal const byte ProofSucceedByPayingAndKeepingTheGoods = 1;
    internal const byte ProofSucceedByPayingAndGivingTheGoodsBack = 2;
    internal const byte ProofBetrayByKeepingTheGoods = 1;
    internal const byte ProofBetrayByGivingTheGoodsBack = 2;
    internal const byte ProofFailByLosingHideoutBattle = 1;

    private static readonly ConditionalWeakTable<Quest, object> ObservedSuccessProof = new();
    private static readonly ConditionalWeakTable<Quest, object> ObservedBetrayalProof = new();
    private static readonly ConditionalWeakTable<Quest, object> ObservedFailProof = new();

    private static byte CaptureQuestSuccessProof(Issue issue)
        => issue.IssueQuest is Quest quest && ObservedSuccessProof.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0;

    private static byte CaptureQuestBetrayalProof(Issue issue)
        => issue.IssueQuest is Quest quest && ObservedBetrayalProof.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0;

    private static byte CaptureQuestFailProof(Issue issue)
        => issue.IssueQuest is Quest quest && ObservedFailProof.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0;

    internal static void ObserveQuestSuccess(Quest quest, byte proof)
    {
        ObservedSuccessProof.Remove(quest);
        ObservedSuccessProof.Add(quest, proof);
    }

    internal static void ObserveQuestBetrayal(Quest quest, byte proof)
    {
        ObservedBetrayalProof.Remove(quest);
        ObservedBetrayalProof.Add(quest, proof);
    }

    internal static void ObserveQuestFail(Quest quest, byte proof)
    {
        ObservedFailProof.Remove(quest);
        ObservedFailProof.Add(quest, proof);
    }

    internal static bool BlockAndReportTerminalOutcome(Quest quest, IssueFinalizeReason reason)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        var owner = quest.QuestGiver;
        if (owner != null &&
            ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) &&
            ownershipRegistry.IsLocalPeerOwner(owner) &&
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider))
        {
            MessageBroker.Instance.Publish(owner, new QuestTerminalOutcomeTriggered(owner, controllerIdProvider.ControllerId, reason));
        }

        return false;
    }

    private static void ApplySucceedByPayingAndKeepingTheGoods(Quest quest)
    {
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, quest._stolenTradeGoodPrice);
        quest.AddLog(quest.SuccessQuestLogText);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Calculating, 100)
        });
        MobileParty.MainParty.ItemRoster.AddToCounts(quest._stolenTradeGood, quest._stolenTradeGoodAmount);
        quest.QuestGiver.AddPower(5f);
        quest._counterOfferHero.AddPower(-5f);
        ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, 10);
        foreach (var notable in quest.QuestGiver.CurrentSettlement.Notables.WhereQ(notable => notable.IsMerchant))
        {
            ChangeRelationAction.ApplyPlayerRelation(notable, -3);
        }
        quest.CompleteQuestWithSuccess();
    }

    private static void ApplySucceedByPayingAndGivingTheGoodsBack(Quest quest)
    {
        GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, quest._stolenTradeGoodPrice);
        quest.AddLog(quest.SuccessByGivingBackTheGoodsQuestLogText);
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest._counterOfferGold);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Calculating, 150)
        });
        quest.QuestGiver.AddPower(5f);
        quest._counterOfferHero.AddPower(5f);
        ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, 10);
        foreach (var notable in quest.QuestGiver.CurrentSettlement.Notables.WhereQ(notable => notable != quest.QuestGiver))
        {
            ChangeRelationAction.ApplyPlayerRelation(notable, 3);
        }
        quest.CompleteQuestWithSuccess();
    }

    private static void ApplyQuestSuccessConsequence(Quest quest)
    {
        switch (QuestSuccessProofContext.Current)
        {
            case ProofSucceedByPayingAndGivingTheGoodsBack:
                ApplySucceedByPayingAndGivingTheGoodsBack(quest);
                return;
            default:
                ApplySucceedByPayingAndKeepingTheGoods(quest);
                return;
        }
    }

    private static void ApplyBetrayByKeepingTheGoods(Quest quest)
    {
        quest.QuestGiver.AddPower(-5f);
        quest._counterOfferHero.AddPower(-5f);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Calculating, 100)
        });
        MobileParty.MainParty.ItemRoster.AddToCounts(quest._stolenTradeGood, quest._stolenTradeGoodAmount);
        ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, -5);
        foreach (var notable in quest.QuestGiver.CurrentSettlement.Notables.WhereQ(notable => notable.IsMerchant))
        {
            ChangeRelationAction.ApplyPlayerRelation(notable, -3);
        }
        quest.CompleteQuestWithBetrayal(quest.FailBetrayTakeGoodsQuestLogText);
    }

    private static void ApplyBetrayByGivingTheGoodsBack(Quest quest)
    {
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest._counterOfferGold);
        quest.QuestGiver.AddPower(-5f);
        quest._counterOfferHero.AddPower(5f);
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Honor, 100)
        });
        ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, -5);
        foreach (var notable in quest.QuestGiver.CurrentSettlement.Notables.WhereQ(notable => notable != quest.QuestGiver))
        {
            ChangeRelationAction.ApplyPlayerRelation(notable, 3);
        }
        quest.CompleteQuestWithBetrayal(quest.FailBetrayQuestLogText);
    }

    private static void ApplyQuestBetrayalConsequence(Quest quest)
    {
        switch (QuestBetrayalProofContext.Current)
        {
            case ProofBetrayByGivingTheGoodsBack:
                ApplyBetrayByGivingTheGoodsBack(quest);
                return;
            default:
                ApplyBetrayByKeepingTheGoods(quest);
                return;
        }
    }

    private static void ApplyQuestFailConsequence(Quest quest)
    {
        quest.QuestGiver.AddPower(-5f);
        quest._counterOfferHero.AddPower(-5f);
        ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, -5);
        quest.CompleteQuestWithFail(quest.FailLoseHideoutFightQuestLogText);
    }

    private static void ApplyQuestCancelConsequence(Quest quest)
    {
        quest.CompleteQuestWithCancel(quest.CancelSettlementOwnerChangedQuestLogText);
    }

    static GangLeaderNeedsToOffloadStolenGoodsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("GangLeaderNeedsToOffloadStolenGoods")
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept(AlternativeAcceptMirror)
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestCancelValidation(ValidateQuestCancel)
            .WithQuestCancelConsequence(ApplyQuestCancelConsequence)
            .WithQuestFailValidation(ValidateQuestFail)
            .WithQuestFailProofCapture(CaptureQuestFailProof)
            .WithQuestFailConsequence(ApplyQuestFailConsequence)
            .WithQuestBetrayalValidation(ValidateQuestBetrayal)
            .WithQuestBetrayalProofCapture(CaptureQuestBetrayalProof)
            .WithQuestBetrayalConsequence(ApplyQuestBetrayalConsequence)
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessProofCapture(CaptureQuestSuccessProof)
            .WithQuestSuccessConsequence(ApplyQuestSuccessConsequence)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}

[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssueQuest))]
internal class GangLeaderNeedsToOffloadStolenGoodsQuestBranchObserverPatches
{
    [HarmonyPatch("SucceedQuestByPayingAndKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndKeepingTheGoodsPrefix(Quest __instance)
    {
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ObserveQuestSuccess(
            __instance, GangLeaderNeedsToOffloadStolenGoodsQuestType.ProofSucceedByPayingAndKeepingTheGoods);
        return GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(__instance, IssueFinalizeReason.QuestSuccess);
    }

    [HarmonyPatch("SucceedQuestByPayingAndGivingTheGoodsBack")]
    [HarmonyPrefix]
    private static bool SucceedQuestByPayingAndGivingTheGoodsBackPrefix(Quest __instance)
    {
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ObserveQuestSuccess(
            __instance, GangLeaderNeedsToOffloadStolenGoodsQuestType.ProofSucceedByPayingAndGivingTheGoodsBack);
        return GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(__instance, IssueFinalizeReason.QuestSuccess);
    }

    [HarmonyPatch("FailQuestByKeepingTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByKeepingTheGoodsPrefix(Quest __instance)
    {
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ObserveQuestBetrayal(
            __instance, GangLeaderNeedsToOffloadStolenGoodsQuestType.ProofBetrayByKeepingTheGoods);
        return GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(__instance, IssueFinalizeReason.QuestBetrayal);
    }

    [HarmonyPatch("FailQuestByGivingBackTheGoods")]
    [HarmonyPrefix]
    private static bool FailQuestByGivingBackTheGoodsPrefix(Quest __instance)
    {
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ObserveQuestBetrayal(
            __instance, GangLeaderNeedsToOffloadStolenGoodsQuestType.ProofBetrayByGivingTheGoodsBack);
        return GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(__instance, IssueFinalizeReason.QuestBetrayal);
    }

    [HarmonyPatch("FailQuestByLosingHideoutBattle")]
    [HarmonyPrefix]
    private static bool FailQuestByLosingHideoutBattlePrefix(Quest __instance)
    {
        GangLeaderNeedsToOffloadStolenGoodsQuestType.ObserveQuestFail(
            __instance, GangLeaderNeedsToOffloadStolenGoodsQuestType.ProofFailByLosingHideoutBattle);
        return GangLeaderNeedsToOffloadStolenGoodsQuestType.BlockAndReportTerminalOutcome(__instance, IssueFinalizeReason.QuestFail);
    }
}
