using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using ProtoBuf;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

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

    public VillageNeedsCraftingMaterialsAcceptFields(int requestedItemAmount, int rewardGold)
    {
        RequestedItemAmount = requestedItemAmount;
        RewardGold = rewardGold;
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
            }
        }

        public bool TryCaptureQuestFields(Hero owner, out VillageNeedsCraftingMaterialsAcceptFields fields)
        {
            fields = default;
            if (owner?.Issue?.IssueQuest is not Quest quest) return false;

            fields = new VillageNeedsCraftingMaterialsAcceptFields(quest._requestedItemAmount, quest.RewardGold);
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

                if (quest._playerAcceptedQuestLog is JournalLog log)
                {
                    JournalLogRangeField.SetValue(log, fields.RequestedItemAmount);
                }
            }
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

    static VillageNeedsCraftingMaterialsQuestType()
    {
        var descriptor = QuestDescriptorBuilder.For<Issue, Quest>("VillageNeedsCraftingMaterials")
            .WithQuestSolutionAccept(QuestSolutionAcceptMirror)
            .WithAlternativeAccept()
            .WithCreationTrigger(OnGenuineCreation)
            .WithQuestSuccessValidation(ValidateQuestSuccess)
            .WithQuestSuccessConsequence(ApplyQuestSuccessConsequence)
            .Build();

        QuestTypeRegistry.Register(descriptor);
    }
}
