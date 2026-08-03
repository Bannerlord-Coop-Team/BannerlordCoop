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

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative quest-solution-accept handling for Landowner Needs Manual Laborers - own bespoke
/// messages/force-write (see <see cref="ILandLordNeedsManualLaborersIssueInterface"/>'s doc comment). Creation
/// rides the shared <see cref="SimpleIssueCreationHandler"/>, alternative-solution-accept rides the fully
/// generic mirror, and removal rides the existing generic finalize choke point.
/// </summary>
internal class LandLordNeedsManualLaborersIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandLordNeedsManualLaborersIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILandLordNeedsManualLaborersIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public LandLordNeedsManualLaborersIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILandLordNeedsManualLaborersIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LandLordManualLaborersIssueQuestAcceptTriggered>(Handle_LandLordManualLaborersIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestLandLordManualLaborersIssueAcceptQuest>(Handle_RequestLandLordManualLaborersIssueAcceptQuest);
        messageBroker.Subscribe<NetworkLandLordManualLaborersIssueQuestAccepted>(Handle_NetworkLandLordManualLaborersIssueQuestAccepted);
        messageBroker.Subscribe<NetworkLandLordManualLaborersIssueAcceptRejected>(Handle_NetworkLandLordManualLaborersIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LandLordManualLaborersIssueQuestAcceptTriggered>(Handle_LandLordManualLaborersIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestLandLordManualLaborersIssueAcceptQuest>(Handle_RequestLandLordManualLaborersIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkLandLordManualLaborersIssueQuestAccepted>(Handle_NetworkLandLordManualLaborersIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkLandLordManualLaborersIssueAcceptRejected>(Handle_NetworkLandLordManualLaborersIssueAcceptRejected);
    }

    private void Handle_LandLordManualLaborersIssueQuestAcceptTriggered(MessagePayload<LandLordManualLaborersIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkLandLordManualLaborersIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.RequestedPrisonerCount, payload.What.MaximumPrisonerCount));
        }
        else
        {
            network.SendAll(new RequestLandLordManualLaborersIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestLandLordManualLaborersIssueAcceptQuest(MessagePayload<RequestLandLordManualLaborersIssueAcceptQuest> payload)
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
                    nameof(RequestLandLordManualLaborersIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkLandLordManualLaborersIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var requestedPrisonerCount, out var maximumPrisonerCount))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkLandLordManualLaborersIssueQuestAccepted(ownerId, player.ControllerId, requestedPrisonerCount, maximumPrisonerCount));
            }
            else
            {
                network.Send(requester, new NetworkLandLordManualLaborersIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkLandLordManualLaborersIssueQuestAccepted(MessagePayload<NetworkLandLordManualLaborersIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.RequestedPrisonerCount, data.MaximumPrisonerCount);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkLandLordManualLaborersIssueAcceptRejected(MessagePayload<NetworkLandLordManualLaborersIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
