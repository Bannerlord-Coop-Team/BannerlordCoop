using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using System.Linq;
using GameInterface.Services.Heroes.HeirSelection.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Handlers;

using Issue = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue;
using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

internal readonly struct ArmyNeedsSuppliesJournalChanged : IEvent
{
    public readonly Quest Quest;
    public ArmyNeedsSuppliesJournalChanged(Quest quest) => Quest = quest;
}

internal sealed class ArmyNeedsSuppliesHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IArmyNeedsSuppliesQuest quests;
    private readonly QuestTypeDescriptor descriptor;

    public ArmyNeedsSuppliesHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IArmyNeedsSuppliesQuest quest, IArmyNeedsSuppliesDelivery delivery)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        quests = quest;
        descriptor = QuestDescriptorBuilder.For<Issue, Quest>("Army Needs Supply")
            .WithQuestSolutionAccept(quest)
            .WithQuestSuccessProofCapture(_ => QuestSuccessProofContext.Current)
            .WithQuestSuccessValidation((issue, party) =>
                delivery.CanDeliver(issue.IssueQuest as Quest, party, QuestSuccessProofContext.Current))
            .WithQuestSuccessConsequence(delivery.Deliver)
            .Build();
        QuestTypeRegistry.Register(descriptor);
        broker.Subscribe<ArmyNeedsSuppliesJournalChanged>(PublishJournal);
        broker.Subscribe<NetworkArmyNeedsSuppliesJournal>(ReceiveJournal);
        broker.Subscribe<PlayerHeirSelectionCompleted>(CancelPredecessorQuests);
        broker.Subscribe<PlayerHeroChanged>(RemoveObserverPresentation);
    }

    public void Dispose()
    {
        broker.Unsubscribe<ArmyNeedsSuppliesJournalChanged>(PublishJournal);
        broker.Unsubscribe<NetworkArmyNeedsSuppliesJournal>(ReceiveJournal);
        broker.Unsubscribe<PlayerHeirSelectionCompleted>(CancelPredecessorQuests);
        broker.Unsubscribe<PlayerHeroChanged>(RemoveObserverPresentation);
        QuestTypeRegistry.Unregister(descriptor);
    }

    private void RemoveObserverPresentation(MessagePayload<PlayerHeroChanged> payload)
    {
        if (ModInformation.IsServer) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>())
        {
            if (quests.IsLocalOwner(quest)) continue;
            quest.ClearRelatedFields();
            quest.RemoveAllTrackedObjects();
            quest.RemoveAllMapMarkers();
        }
    }

    private void CancelPredecessorQuests(MessagePayload<PlayerHeirSelectionCompleted> payload)
    {
        if (ModInformation.IsClient) return;
        foreach (var quest in Campaign.Current.QuestManager.Quests.OfType<Quest>().ToArray())
        {
            if (!quests.IsOwner(quest, payload.What.PlayerHero)) continue;
            quest.CompleteQuestWithCancel(new TextObject("{=bYdhYidf}The quest was canceled because your clan leader, who made the original agreement, is no longer head of the clan.\""));
        }
    }

    private void PublishJournal(MessagePayload<ArmyNeedsSuppliesJournalChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (quest._grainLog == null ||
            !objects.TryGetIdWithLogging(quest.QuestGiver, out var ownerId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;
        network.SendAll(new NetworkArmyNeedsSuppliesJournal
        {
            OwnerId = ownerId,
            Generation = generation,
            QuestId = quest.StringId,
            Journal = new ArmyNeedsSuppliesJournal(quest),
        });
    }

    private void ReceiveJournal(MessagePayload<NetworkArmyNeedsSuppliesJournal> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) ||
                !generations.TryGetGeneration(owner, out var generation) || generation != data.Generation ||
                owner.Issue?.IssueQuest is not Quest quest || quest.StringId != data.QuestId) return;
            using (new AllowedThread())
            {
                data.Journal?.Apply(quest);
                if (quests.IsLocalOwner(quest)) CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, true);
            }
        });
    }
}
