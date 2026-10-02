using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

internal sealed class HeadmanHerdJournalHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanHerdJournalHandler>();
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;

    public HeadmanHerdJournalHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IIssueGenerationRegistry generations, IIssueOwnershipRegistry ownership)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.generations = generations;
        this.ownership = ownership;
        broker.Subscribe<HeadmanHerdJournalAdded>(HandleAdded);
        broker.Subscribe<NetworkHeadmanHerdJournalAdded>(HandleReceived);
    }

    public void Dispose()
    {
        broker.Unsubscribe<HeadmanHerdJournalAdded>(HandleAdded);
        broker.Unsubscribe<NetworkHeadmanHerdJournalAdded>(HandleReceived);
    }

    private void HandleAdded(MessagePayload<HeadmanHerdJournalAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        var owner = quest.QuestGiver;
        // Acceptance mirrors the initial log before ownership is assigned.
        if (!ownership.TryGetOwnerControllerId(owner, out _)) return;
        if (!generations.TryGetGeneration(owner, out var generation)) return;
        if (!objects.TryGetIdWithLogging(owner, out var ownerId)) return;
        network.SendAll(new NetworkHeadmanHerdJournalAdded(ownerId, generation, quest.StringId,
            quest.JournalEntries.Count - 1, CampaignTime.Now, payload.What.Text, payload.What.HideInformation));
    }

    private void HandleReceived(MessagePayload<NetworkHeadmanHerdJournalAdded> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!generations.TryGetGeneration(owner, out var generation) || generation != data.Generation) return;
            if (owner.Issue?.IssueQuest is not Quest quest || quest.StringId != data.QuestId) return;
            if (data.Index < quest.JournalEntries.Count) return;
            if (data.Index != quest.JournalEntries.Count)
            {
                Logger.Error("Herd quest {Quest} expected journal entry {Expected}, received {Actual}",
                    data.QuestId, quest.JournalEntries.Count, data.Index);
                return;
            }
            quest._journalEntries.Add(new JournalLog(data.Time, data.Text));
            CampaignEventDispatcher.Instance.OnQuestLogAdded(quest, data.HideInformation);
        });
    }
}
