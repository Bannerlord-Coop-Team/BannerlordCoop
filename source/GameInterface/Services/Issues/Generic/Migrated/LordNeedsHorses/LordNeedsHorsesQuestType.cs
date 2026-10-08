using Common.Messaging;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Generic.Migrated.LordNeedsHorses;

using Issue = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue;
using Quest = LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssueQuest;

[QuestTypeModule]
internal static class LordNeedsHorsesQuestType
{
    internal static readonly ConditionalWeakTable<PartyScreenLogic, Issue> TroopSelections = new();
    internal static readonly ConditionalWeakTable<ConversationManager, Hero> PendingAcceptances = new();

    private sealed class AcceptStrategy : IRaceArbitratedAcceptMirrorStrategy<LordNeedsHorsesAcceptFields>,
        IAlternativeAcceptMirrorStrategy<NetworkLordNeedsHorsesJournal>
    {
        private ILordNeedsHorsesQuest Service
        {
            get
            {
                if (!ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service))
                    throw new InvalidOperationException("Lord Needs Horses quest service is unavailable");
                return service;
            }
        }
        public void ReplayQuestAccepted(Hero owner) => Service.ReplayQuestAccepted(owner);
        public bool TryCaptureQuestFields(Hero owner, out LordNeedsHorsesAcceptFields fields) => Service.TryCaptureQuestFields(owner, out fields);
        public void MirrorQuestAccepted(Hero owner, LordNeedsHorsesAcceptFields fields) => Service.MirrorQuestAccepted(owner, fields);
        public void RejectAcceptance(Hero owner) =>
            ((IRaceArbitratedAcceptMirrorStrategy<LordNeedsHorsesAcceptFields>)Service).RejectAcceptance(owner);
        public void ReplayAlternativeAccepted(Hero owner) => Service.ReplayAlternativeAccepted(owner);
        public bool TryCaptureAlternativeFields(Hero owner, out NetworkLordNeedsHorsesJournal fields) =>
            Service.TryCaptureAlternativeFields(owner, out fields);
        public void MirrorAlternativeAccepted(Hero owner, NetworkLordNeedsHorsesJournal fields) =>
            Service.MirrorAlternativeAccepted(owner, fields);
        void IAlternativeAcceptMirrorStrategy<NetworkLordNeedsHorsesJournal>.RejectAcceptance(Hero owner) =>
            ((IAlternativeAcceptMirrorStrategy<NetworkLordNeedsHorsesJournal>)Service).RejectAcceptance(owner);
    }

    static LordNeedsHorsesQuestType()
    {
        var acceptance = new AcceptStrategy();
        QuestTypeRegistry.Register(QuestDescriptorBuilder.For<Issue, Quest>("LordNeedsHorses")
            .WithCreationTrigger(issue => MessageBroker.Instance.Publish(issue.IssueOwner, new LordNeedsHorsesIssueCreated(issue)))
            .WithQuestSolutionAccept(acceptance)
            .WithQuestSolutionAcceptTrigger((owner, _) =>
            {
                if (ContainerProvider.TryResolve<ILordNeedsHorsesQuest>(out var service)) service.WaitForAcceptance(owner);
            })
            .WithAlternativeAccept(acceptance)
            .WithQuestSuccessValidation((issue, party) => party != null && issue.IssueQuest is Quest quest &&
                CountMounts(party.ItemRoster, quest._mountObjectToBeDelivered) >= quest._numMountsToBeDelivered)
            .WithQuestFailValidation(issue => issue.IssueQuest is Quest { IsOngoing: true })
            .WithQuestFailConsequence(quest => quest.OnQuestDeclined())
            .Build());
    }

    // Matches the quest's own GetNumQuestMountsInInventory: every stack counts, while GetItemNumber reads only the first.
    internal static int CountMounts(ItemRoster roster, ItemObject mount) =>
        roster.Where(element => element.EquipmentElement.Item == mount).Sum(element => element.Amount);
}
