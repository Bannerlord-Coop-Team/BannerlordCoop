using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsTools;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

internal class VillageNeedsToolsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageNeedsToolsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public VillageNeedsToolsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Subscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageIssueCreated>(Handle_VillageIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageIssueCreated>(Handle_NetworkVillageIssueCreated);
    }

    private void Handle_VillageIssueCreated(MessagePayload<VillageIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!VillageNeedsToolsQuestType.CreationCapture.TryCapture(issue, out var fields))
        {
            Logger.Error("Could not capture Village Needs Tools issue fields for owner {Owner}", ownerId);
            return;
        }
        var (requestedItem, exchangeItem, numberOfExchangeItem, numberOfRequestedItem, payment) = fields;

        if (!objectManager.TryGetIdWithLogging(requestedItem, out var requestedItemId)) return;

        string exchangeItemId = null;
        if (exchangeItem != null && !objectManager.TryGetIdWithLogging(exchangeItem, out exchangeItemId)) return;

        var generation = IssueGenerationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkVillageIssueCreated(
            ownerId, requestedItemId, exchangeItemId, numberOfRequestedItem, numberOfExchangeItem, payment, generation));
    }

    private void Handle_NetworkVillageIssueCreated(MessagePayload<NetworkVillageIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            IssueGenerationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            ItemObject exchangeItem = null;
            if (data.ExchangeItemId != null &&
                !objectManager.TryGetObjectWithLogging<ItemObject>(data.ExchangeItemId, out exchangeItem)) return;

            VillageNeedsToolsQuestType.CreationCapture.ConstructAndRegisterReplicated(
                owner, (requestedItem, exchangeItem, data.NumberOfExchangeItem, data.NumberOfRequestedItem, data.Payment));
        });
    }
}
