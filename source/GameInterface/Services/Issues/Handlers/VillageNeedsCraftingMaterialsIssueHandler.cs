using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

internal class VillageNeedsCraftingMaterialsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsCraftingMaterialsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IIssueGenerationRegistry generationRegistry;

    public VillageNeedsCraftingMaterialsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;
        this.generationRegistry = generationRegistry;

        messageBroker.Subscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Subscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);
        messageBroker.Subscribe<VillageCraftingProgressChanged>(Handle_VillageCraftingProgressChanged);
        messageBroker.Subscribe<NetworkVillageCraftingProgressChanged>(Handle_NetworkVillageCraftingProgressChanged);
        messageBroker.Subscribe<PlayerInventoryExchangeApplied>(Handle_PlayerInventoryExchangeApplied);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);
        messageBroker.Unsubscribe<VillageCraftingProgressChanged>(Handle_VillageCraftingProgressChanged);
        messageBroker.Unsubscribe<NetworkVillageCraftingProgressChanged>(Handle_NetworkVillageCraftingProgressChanged);
        messageBroker.Unsubscribe<PlayerInventoryExchangeApplied>(Handle_PlayerInventoryExchangeApplied);
    }

    private void Handle_PlayerInventoryExchangeApplied(MessagePayload<PlayerInventoryExchangeApplied> payload)
    {
        if (ModInformation.IsClient) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Hero, out var heroId)) return;

        foreach (var quest in Campaign.Current.QuestManager.Quests
            .OfType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>().ToList())
        {
            if (!quest.IsOngoing || quest.QuestGiver.Issue?.IssueQuest != quest) continue;
            if (!ownershipRegistry.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId)) continue;
            if (!playerManager.TryGetPlayer(controllerId, out var player) || player.HeroId != heroId) continue;

            quest.UpdateQuestLog();
        }
    }

    private void Handle_VillageCraftingProgressChanged(MessagePayload<VillageCraftingProgressChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (!quest.IsOngoing || quest.QuestGiver.Issue?.IssueQuest != quest) return;
        if (!objectManager.TryGetIdWithLogging(quest.QuestGiver, out var ownerId)) return;
        if (!generationRegistry.TryGetGeneration(quest.QuestGiver, out var generation)) return;

        network.SendAll(new NetworkVillageCraftingProgressChanged(ownerId, generation, quest._playerAcceptedQuestLog.CurrentProgress));
    }

    private void Handle_NetworkVillageCraftingProgressChanged(MessagePayload<NetworkVillageCraftingProgressChanged> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!generationRegistry.TryGetGeneration(owner, out var generation) || generation != data.Generation) return;
            if (owner.Issue?.IssueQuest is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest ||
                !owner.Issue.IsSolvingWithQuest || quest._playerAcceptedQuestLog == null) return;
            if (data.Progress < 0 || data.Progress > quest._requestedItemAmount) return;

            quest._playerAcceptedQuestLog.UpdateCurrentProgress(data.Progress);
            quest.CheckIfPlayerReadyToReturnItems();
        });
    }

    private void Handle_VillageCraftingIssueCreated(MessagePayload<VillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!VillageNeedsCraftingMaterialsQuestType.CreationCapture.TryCapture(issue, out var requestedItem))
        {
            Logger.Error("Could not capture Village Needs Crafting Materials issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(requestedItem, out var requestedItemId)) return;

        var generation = generationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkVillageCraftingIssueCreated(ownerId, requestedItemId, generation));
    }

    private void Handle_NetworkVillageCraftingIssueCreated(MessagePayload<NetworkVillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            generationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            VillageNeedsCraftingMaterialsQuestType.CreationCapture.ConstructAndRegisterReplicated(owner, requestedItem);
        });
    }
}
