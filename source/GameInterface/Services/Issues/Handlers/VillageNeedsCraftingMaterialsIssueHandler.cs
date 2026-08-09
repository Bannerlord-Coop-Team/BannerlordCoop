using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
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

    public VillageNeedsCraftingMaterialsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;

        messageBroker.Subscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Subscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);

        messageBroker.Subscribe<VillageCraftingIssueAlternativeSolutionCompletionRequested>(Handle_VillageCraftingIssueAlternativeSolutionCompletionRequested);
        messageBroker.Subscribe<RequestVillageCraftingIssueAlternativeSolutionCompletion>(Handle_RequestVillageCraftingIssueAlternativeSolutionCompletion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);

        messageBroker.Unsubscribe<VillageCraftingIssueAlternativeSolutionCompletionRequested>(Handle_VillageCraftingIssueAlternativeSolutionCompletionRequested);
        messageBroker.Unsubscribe<RequestVillageCraftingIssueAlternativeSolutionCompletion>(Handle_RequestVillageCraftingIssueAlternativeSolutionCompletion);
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

        var generation = IssueGenerationRegistry.Bump(issue.IssueOwner);

        network.SendAll(new NetworkVillageCraftingIssueCreated(ownerId, requestedItemId, generation));
    }

    private void Handle_NetworkVillageCraftingIssueCreated(MessagePayload<NetworkVillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            IssueGenerationRegistry.SetGeneration(owner, data.Generation);

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            VillageNeedsCraftingMaterialsQuestType.CreationCapture.ConstructAndRegisterReplicated(owner, requestedItem);
        });
    }

    private void Handle_VillageCraftingIssueAlternativeSolutionCompletionRequested(
        MessagePayload<VillageCraftingIssueAlternativeSolutionCompletionRequested> payload)
    {
        if (ModInformation.IsServer) return;

        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        network.SendAll(new RequestVillageCraftingIssueAlternativeSolutionCompletion(ownerId));
    }

    private void Handle_RequestVillageCraftingIssueAlternativeSolutionCompletion(
        MessagePayload<RequestVillageCraftingIssueAlternativeSolutionCompletion> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as LiteNetLib.NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageCraftingIssueAlternativeSolutionCompletion), ownerId);
                return;
            }

            if (!IssueOwnershipRegistry.TryGetOwnerControllerId(owner, out var recordedControllerId) ||
                recordedControllerId != player.ControllerId)
            {
                Logger.Error("Rejecting {Message} from {Requester}, not the recorded owner for {Owner}",
                    nameof(RequestVillageCraftingIssueAlternativeSolutionCompletion), player.ControllerId, ownerId);
                return;
            }

            if (owner.Issue is not VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue issue) return;
            if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return;

            AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
        });
    }
}
