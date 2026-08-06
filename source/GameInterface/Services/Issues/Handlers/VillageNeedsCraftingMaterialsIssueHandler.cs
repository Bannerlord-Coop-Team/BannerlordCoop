using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
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

        messageBroker.Subscribe<VillageCraftingIssueQuestAcceptTriggered>(Handle_VillageCraftingIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestVillageCraftingIssueAcceptQuest>(Handle_RequestVillageCraftingIssueAcceptQuest);
        messageBroker.Subscribe<NetworkVillageCraftingIssueQuestAccepted>(Handle_NetworkVillageCraftingIssueQuestAccepted);

        messageBroker.Subscribe<VillageCraftingIssueAlternativeAcceptTriggered>(Handle_VillageCraftingIssueAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestVillageCraftingIssueAcceptAlternative>(Handle_RequestVillageCraftingIssueAcceptAlternative);
        messageBroker.Subscribe<NetworkVillageCraftingIssueAlternativeAccepted>(Handle_NetworkVillageCraftingIssueAlternativeAccepted);

        messageBroker.Subscribe<NetworkVillageCraftingIssueAcceptRejected>(Handle_NetworkVillageCraftingIssueAcceptRejected);

        messageBroker.Subscribe<VillageCraftingIssueAlternativeSolutionCompletionRequested>(Handle_VillageCraftingIssueAlternativeSolutionCompletionRequested);
        messageBroker.Subscribe<RequestVillageCraftingIssueAlternativeSolutionCompletion>(Handle_RequestVillageCraftingIssueAlternativeSolutionCompletion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageCraftingIssueCreated>(Handle_VillageCraftingIssueCreated);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueCreated>(Handle_NetworkVillageCraftingIssueCreated);

        messageBroker.Unsubscribe<VillageCraftingIssueQuestAcceptTriggered>(Handle_VillageCraftingIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageCraftingIssueAcceptQuest>(Handle_RequestVillageCraftingIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueQuestAccepted>(Handle_NetworkVillageCraftingIssueQuestAccepted);

        messageBroker.Unsubscribe<VillageCraftingIssueAlternativeAcceptTriggered>(Handle_VillageCraftingIssueAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestVillageCraftingIssueAcceptAlternative>(Handle_RequestVillageCraftingIssueAcceptAlternative);
        messageBroker.Unsubscribe<NetworkVillageCraftingIssueAlternativeAccepted>(Handle_NetworkVillageCraftingIssueAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkVillageCraftingIssueAcceptRejected>(Handle_NetworkVillageCraftingIssueAcceptRejected);

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

        network.SendAll(new NetworkVillageCraftingIssueCreated(ownerId, requestedItemId));
    }

    private void Handle_NetworkVillageCraftingIssueCreated(MessagePayload<NetworkVillageCraftingIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;

            if (owner.Issue != null) return;

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.RequestedItemId, out var requestedItem)) return;

            VillageNeedsCraftingMaterialsQuestType.CreationCapture.ConstructAndRegisterReplicated(owner, requestedItem);
        });
    }

    private void Handle_VillageCraftingIssueQuestAcceptTriggered(MessagePayload<VillageCraftingIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageCraftingIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.RequestedItemAmount, payload.What.RewardGold));
        }
        else
        {
            network.SendAll(new RequestVillageCraftingIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestVillageCraftingIssueAcceptQuest(MessagePayload<RequestVillageCraftingIssueAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageCraftingIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
                return;
            }

            var canAccept = owner.Issue is VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue && owner.Issue.IsOngoingWithoutQuest;
            if (VillageNeedsCraftingMaterialsQuestType.QuestSolutionAccept.TryArbitrate(owner, _ => canAccept, out var fields))
            {
                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageCraftingIssueQuestAccepted(ownerId, player.ControllerId, fields.RequestedItemAmount, fields.RewardGold));
            }
            else
            {
                if (canAccept)
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields - rolled back and rejecting", ownerId);
                }
                network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageCraftingIssueQuestAccepted(MessagePayload<NetworkVillageCraftingIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            VillageNeedsCraftingMaterialsQuestType.QuestSolutionAccept.Mirror(owner, (data.RequestedItemAmount, data.RewardGold));
            IssueOwnershipRegistry.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_VillageCraftingIssueAlternativeAcceptTriggered(MessagePayload<VillageCraftingIssueAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkVillageCraftingIssueAlternativeAccepted(ownerId, hostControllerId, payload.What.State));
        }
        else
        {
            network.SendAll(new RequestVillageCraftingIssueAcceptAlternative(ownerId, payload.What.State));
        }
    }

    private void Handle_RequestVillageCraftingIssueAcceptAlternative(MessagePayload<RequestVillageCraftingIssueAcceptAlternative> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestVillageCraftingIssueAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                var state = payload.What.ToState();
                VillageNeedsCraftingMaterialsQuestType.AlternativeAccept.Mirror(owner, state);
                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkVillageCraftingIssueAlternativeAccepted(ownerId, player.ControllerId, state));
            }
            else
            {
                network.Send(requester, new NetworkVillageCraftingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkVillageCraftingIssueAlternativeAccepted(MessagePayload<NetworkVillageCraftingIssueAlternativeAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            VillageNeedsCraftingMaterialsQuestType.AlternativeAccept.Mirror(owner, data.ToState());
            IssueOwnershipRegistry.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkVillageCraftingIssueAcceptRejected(MessagePayload<NetworkVillageCraftingIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            VillageNeedsCraftingMaterialsQuestType.QuestSolutionAccept.Reject(owner);
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
        var requester = payload.Who as NetPeer;
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
