using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
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

/// <summary>
/// Server-authoritative creation AND quest-solution-accept handling for Village Needs Draught Animals - own
/// bespoke messages/force-write for both (see
/// <see cref="IHeadmanVillageNeedsDraughtAnimalsIssueInterface"/>'s doc comment: this type needs a creation-time
/// capture the survey missed, on top of the accept-time discount roll it flagged). Alternative-solution-accept
/// rides the fully generic mirror, and removal rides the existing generic finalize choke point.
/// </summary>
internal class HeadmanVillageNeedsDraughtAnimalsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<HeadmanVillageNeedsDraughtAnimalsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IHeadmanVillageNeedsDraughtAnimalsIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public HeadmanVillageNeedsDraughtAnimalsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IHeadmanVillageNeedsDraughtAnimalsIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<HeadmanVillageNeedsDraughtAnimalsIssueCreated>(Handle_HeadmanVillageNeedsDraughtAnimalsIssueCreated);
        messageBroker.Subscribe<NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated>(Handle_NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated);

        messageBroker.Subscribe<HeadmanDraughtAnimalsIssueQuestAcceptTriggered>(Handle_HeadmanDraughtAnimalsIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestHeadmanDraughtAnimalsIssueAcceptQuest>(Handle_RequestHeadmanDraughtAnimalsIssueAcceptQuest);
        messageBroker.Subscribe<NetworkHeadmanDraughtAnimalsIssueQuestAccepted>(Handle_NetworkHeadmanDraughtAnimalsIssueQuestAccepted);
        messageBroker.Subscribe<NetworkHeadmanDraughtAnimalsIssueAcceptRejected>(Handle_NetworkHeadmanDraughtAnimalsIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<HeadmanVillageNeedsDraughtAnimalsIssueCreated>(Handle_HeadmanVillageNeedsDraughtAnimalsIssueCreated);
        messageBroker.Unsubscribe<NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated>(Handle_NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated);

        messageBroker.Unsubscribe<HeadmanDraughtAnimalsIssueQuestAcceptTriggered>(Handle_HeadmanDraughtAnimalsIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestHeadmanDraughtAnimalsIssueAcceptQuest>(Handle_RequestHeadmanDraughtAnimalsIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkHeadmanDraughtAnimalsIssueQuestAccepted>(Handle_NetworkHeadmanDraughtAnimalsIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkHeadmanDraughtAnimalsIssueAcceptRejected>(Handle_NetworkHeadmanDraughtAnimalsIssueAcceptRejected);
    }

    // --- Creation ---

    private void Handle_HeadmanVillageNeedsDraughtAnimalsIssueCreated(MessagePayload<HeadmanVillageNeedsDraughtAnimalsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var selectedAnimal, out var requestedAnimalAmount, out var isQuestWithMeatOffer))
        {
            Logger.Error("Could not capture Village Needs Draught Animals issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(selectedAnimal, out var selectedAnimalId)) return;

        network.SendAll(new NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated(ownerId, selectedAnimalId, requestedAnimalAmount, isQuestWithMeatOffer));
    }

    private void Handle_NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated(MessagePayload<NetworkHeadmanVillageNeedsDraughtAnimalsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<ItemObject>(data.SelectedAnimalId, out var selectedAnimal)) return;

            var replicated = issueInterface.ConstructReplicated(owner, selectedAnimal, data.RequestedAnimalAmount, data.IsQuestWithMeatOffer);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Acceptance ---

    private void Handle_HeadmanDraughtAnimalsIssueQuestAcceptTriggered(MessagePayload<HeadmanDraughtAnimalsIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkHeadmanDraughtAnimalsIssueQuestAccepted(ownerId, hostControllerId, payload.What.DiscountValue));
        }
        else
        {
            network.SendAll(new RequestHeadmanDraughtAnimalsIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestHeadmanDraughtAnimalsIssueAcceptQuest(MessagePayload<RequestHeadmanDraughtAnimalsIssueAcceptQuest> payload)
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
                    nameof(RequestHeadmanDraughtAnimalsIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkHeadmanDraughtAnimalsIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var discountValue))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkHeadmanDraughtAnimalsIssueQuestAccepted(ownerId, player.ControllerId, discountValue));
            }
            else
            {
                network.Send(requester, new NetworkHeadmanDraughtAnimalsIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkHeadmanDraughtAnimalsIssueQuestAccepted(MessagePayload<NetworkHeadmanDraughtAnimalsIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.DiscountValue);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkHeadmanDraughtAnimalsIssueAcceptRejected(MessagePayload<NetworkHeadmanDraughtAnimalsIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
