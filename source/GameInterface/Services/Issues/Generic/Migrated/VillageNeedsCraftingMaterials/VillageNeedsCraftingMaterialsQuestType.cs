using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Helpers;
using ProtoBuf;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;

using Issue = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;
using Quest = VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

[ProtoContract(SkipConstructor = true)]
internal readonly struct VillageNeedsCraftingMaterialsAcceptFields
{
    [ProtoMember(1)]
    public readonly int RequestedItemAmount;
    [ProtoMember(2)]
    public readonly int RewardGold;
    [ProtoMember(3)]
    public readonly int CurrentProgress;

    public VillageNeedsCraftingMaterialsAcceptFields(int requestedItemAmount, int rewardGold, int currentProgress)
    {
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
        CurrentProgress = currentProgress;
    }
}

[QuestTypeModule]
internal static class VillageNeedsCraftingMaterialsQuestType
{
    private static readonly FieldInfo RequestedItemField = AccessTools.Field(typeof(Issue), "_requestedItem");
    private static readonly FieldInfo RequestedItemAmountField = AccessTools.Field(typeof(Quest), "_requestedItemAmount");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));
    private static readonly FieldInfo JournalLogRangeField = AccessTools.Field(typeof(JournalLog), nameof(JournalLog.Range));

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

    private sealed class QuestSolutionAcceptMirrorStrategy : IRaceArbitratedAcceptMirrorStrategy<VillageNeedsCraftingMaterialsAcceptFields>
    {
        public void ReplayQuestAccepted(Hero owner)
        {
            if (owner?.Issue is not Issue || !owner.Issue.IsOngoingWithoutQuest) return;

            using (new QuestSolutionStartAuthorityGuard())
            using (new Generic.Dispatch.IssueDispatchReplayGuard())
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);

                if (owner.Issue.IssueQuest is Quest quest && quest._playerAcceptedQuestLog == null && MobileParty.MainParty != null)
                {
                    quest.QuestAcceptedConsequences();
                }
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out VillageNeedsCraftingMaterialsAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest) return false;

            fields = new VillageNeedsCraftingMaterialsAcceptFields(
                quest._requestedItemAmount, quest.RewardGold, quest._playerAcceptedQuestLog?.CurrentProgress ?? 0);
            return true;
        }

        public void MirrorQuestAccepted(Hero owner, VillageNeedsCraftingMaterialsAcceptFields fields)
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

                RequestedItemAmountField.SetValue(quest, fields.RequestedItemAmount);
                RewardGoldField.SetValue(quest, fields.RewardGold);

                EnsureAcceptedQuestLog(owner, quest, fields);

                if (quest._playerAcceptedQuestLog is JournalLog log)
                {
                    JournalLogRangeField.SetValue(log, fields.RequestedItemAmount);
                    log.UpdateCurrentProgress(fields.CurrentProgress);
                }
            }
        }

        private static void EnsureAcceptedQuestLog(Hero owner, Quest quest, VillageNeedsCraftingMaterialsAcceptFields fields)
        {
            if (quest._playerAcceptedQuestLog != null) return;

            var isLocalPeerOwner = ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) &&
                ownershipRegistry.IsLocalPeerOwner(owner);
            if (isLocalPeerOwner && MobileParty.MainParty != null)
            {
                quest.QuestAcceptedConsequences();
                return;
            }

            var taskName = new TextObject("{=nAEhfGJk}Collect {ITEM}");
            taskName.SetTextVariable("ITEM", quest._requestedItem.Name);
            quest._playerAcceptedQuestLog = quest.AddDiscreteLog(
                quest.QuestStartedLogText, taskName, fields.CurrentProgress, fields.RequestedItemAmount);
        }

        public void RejectAcceptance(Hero owner) => RejectAcceptanceCore(owner);
    }

    private static readonly ICreationCaptureStrategy<Issue, ItemObject> CreationCaptureStrategy =
        new FieldForceCreationCapture<Issue, ItemObject>(RequestedItemField, owner => new Issue(owner));

    private static readonly IRaceArbitratedAcceptMirrorStrategy<VillageNeedsCraftingMaterialsAcceptFields> QuestSolutionAcceptMirror =
        new QuestSolutionAcceptMirrorStrategy();

    public static readonly CreationCaptureRunner<Issue, ItemObject> CreationCapture = new(CreationCaptureStrategy);

    public static readonly RaceArbitratedAcceptMirrorHandler<VillageNeedsCraftingMaterialsAcceptFields> QuestSolutionAccept =
        new(QuestSolutionAcceptMirror);

    private static void OnGenuineCreation(Issue issue)
    {
        MessageBroker.Instance.Publish(issue.IssueOwner, new VillageCraftingIssueCreated(issue));
    }

    private static bool ValidateQuestSuccess(Issue issue, MobileParty party)
    {
        if (party == null) return false;
        if (issue.IssueQuest is not Quest quest) return false;

        return party.ItemRoster.GetItemNumber(quest._requestedItem) >= quest._requestedItemAmount;
    }

    internal const byte ProofFailTimeout = 1;
    internal const byte ProofFailCoercion = 2;
    internal const byte ProofFailWar = 3;

    private static readonly ConditionalWeakTable<Quest, object> ObservedFailProof = new();

    internal static void ObserveQuestFail(Quest quest, byte proof)
    {
        ObservedFailProof.Remove(quest);
        ObservedFailProof.Add(quest, proof);
    }

    internal static void PublishTerminalOutcome(Hero owner, IssueFinalizeReason reason, string controllerId = null)
    {
        if (controllerId == null)
        {
            ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
            controllerId = controllerIdProvider?.ControllerId;
        }

        MessageBroker.Instance.Publish(owner, new QuestTerminalOutcomeTriggered(owner, controllerId, reason));
    }

    internal static void PublishQuestFail(Quest quest, byte proof, string controllerId = null)
    {
        ObserveQuestFail(quest, proof);
        PublishTerminalOutcome(quest.QuestGiver, IssueFinalizeReason.QuestFail, controllerId);
    }

    private static byte CaptureQuestFailProof(Issue issue)
        => issue.IssueQuest is Quest quest && ObservedFailProof.TryGetValue(quest, out var proof) ? (byte)proof : (byte)0;

    private static bool TryResolveRecordedOwner(Issue issue, out Hero ownerHero, out MobileParty ownerParty)
    {
        ownerHero = null;
        ownerParty = null;
        if (!ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) ||
            !ownershipRegistry.TryGetOwnerControllerId(issue.IssueOwner, out var controllerId)) return false;
        if (!ContainerProvider.TryResolve<IPlayerManager>(out var playerManager) ||
            !playerManager.TryGetPlayer(controllerId, out var player)) return false;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return false;

        objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out ownerHero);
        objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out ownerParty);
        return ownerHero != null;
    }

    private static bool IsAtWarWithRecordedOwner(Issue issue)
        => TryResolveRecordedOwner(issue, out var ownerHero, out _) &&
           issue.IssueOwner.MapFaction is { } giverFaction &&
           ownerHero.MapFaction is { } ownerFaction &&
           giverFaction.IsAtWarWith(ownerFaction);

    private static bool IsBeingCoercedByRecordedOwner(Issue issue)
    {
        if (!TryResolveRecordedOwner(issue, out _, out var ownerParty) || ownerParty == null) return false;

        var mapEvent = issue.IssueOwner.CurrentSettlement?.Party?.MapEvent;
        return mapEvent != null &&
               (mapEvent.IsForcingSupplies || mapEvent.IsForcingVolunteers) &&
               mapEvent.AttackerSide.LeaderParty == ownerParty.Party;
    }

    private static bool ValidateQuestCancel(Issue issue)
        => issue.IssueOwner.CurrentSettlement?.IsRaided == true || IsAtWarWithRecordedOwner(issue);

    private static bool ValidateQuestFail(Issue issue)
    {
        if (issue.IssueQuest is not Quest quest) return false;

        return QuestFailProofContext.Current switch
        {
            ProofFailTimeout => quest.QuestDueTime.IsPast,
            ProofFailCoercion => IsBeingCoercedByRecordedOwner(issue),
            ProofFailWar => IsAtWarWithRecordedOwner(issue),
            _ => false,
        };
    }

    private static void ApplyQuestSuccessConsequence(Quest quest)
    {
        quest.AddLog(quest.QuestSuccessLogText, false);
        var itemRosterElement = new ItemRosterElement(quest._requestedItem, quest._requestedItemAmount, null);
        GiveItemAction.ApplyForParties(PartyBase.MainParty, quest.QuestGiver.CurrentSettlement.Party, itemRosterElement);
        GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, quest.RewardGold, false);
        quest.QuestGiver.AddPower(10f);
        quest.RelationshipChangeWithQuestGiver = 5;
        quest.QuestGiver.CurrentSettlement.Village.Hearth += 30f;
        quest.CompleteQuestWithSuccess();
    }

    private static void ApplyQuestSuccessLocalOwnerConsequence(Quest quest)
    {
        TraitLevelingHelper.OnIssueSolvedThroughQuest(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Honor, 30)
        });
    }

    private static void ApplyQuestFailConsequence(Quest quest)
    {
        switch (QuestFailProofContext.Current)
        {
            case ProofFailTimeout:
                quest.AddLog(quest.QuestFailedWithTimeOutLogText, false);
                quest.QuestGiver.AddPower(-10f);
                quest.RelationshipChangeWithQuestGiver = -5;
                quest.QuestGiver.CurrentSettlement.Village.Hearth += -40f;
                quest.CompleteQuestWithFail();
                return;
            case ProofFailCoercion:
                var accusedLog = new TextObject("{=tWZ4a8Ih}You are accused in {SETTLEMENT} of a crime and {QUEST_GIVER.LINK} no longer trusts you in this matter.");
                accusedLog.SetTextVariable("SETTLEMENT", quest.QuestGiver.CurrentSettlement.EncyclopediaLinkWithName);
                StringHelpers.SetCharacterProperties("QUEST_GIVER", quest.QuestGiver.CharacterObject, accusedLog);
                quest.CompleteQuestWithFail(accusedLog);
                ChangeRelationAction.ApplyPlayerRelation(quest.QuestGiver, -5);
                quest.QuestGiver.AddPower(-10f);
                return;
            default:
                quest.CompleteQuestWithFail(quest.QuestCanceledWarDeclaredLogText);
                return;
        }
    }

    private static void ApplyQuestFailLocalOwnerConsequence(Quest quest, byte proof)
    {
        if (proof != ProofFailCoercion) return;

        TraitLevelingHelper.OnIssueSolvedThroughAlternativeSolution(Hero.MainHero, new Tuple<TraitObject, int>[1]
        {
            new Tuple<TraitObject, int>(DefaultTraits.Honor, -50)
        });
    }

    static VillageNeedsCraftingMaterialsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsCraftingMaterials")
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept()
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessConsequence(ApplyQuestSuccessConsequence)
            .WithQuestSuccessLocalOwnerConsequence(ApplyQuestSuccessLocalOwnerConsequence)
            .WithQuestCancelValidation(ValidateQuestCancel)
            .WithQuestFailValidation(ValidateQuestFail)
            .WithQuestFailProofCapture(CaptureQuestFailProof)
            .WithQuestFailConsequence(ApplyQuestFailConsequence)
            .WithQuestFailLocalOwnerConsequence(ApplyQuestFailLocalOwnerConsequence)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
