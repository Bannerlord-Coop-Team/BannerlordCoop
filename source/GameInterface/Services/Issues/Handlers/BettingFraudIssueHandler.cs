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
/// Server-authoritative quest-solution-accept handling for Betting Fraud - own bespoke messages/force-write
/// for the accept-time <c>_thug</c> roll (see <see cref="IBettingFraudIssueInterface"/>'s doc comment).
/// Creation rides the shared <see cref="SimpleIssueCreationHandler"/> (the Issue ctor rolls nothing); this
/// issue type has no alternative solution (<c>IsThereAlternativeSolution == false</c>) and removal rides the
/// existing generic finalize choke point.
/// </summary>
internal class BettingFraudIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BettingFraudIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IBettingFraudIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public BettingFraudIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IBettingFraudIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<BettingFraudIssueQuestAcceptTriggered>(Handle_BettingFraudIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestBettingFraudIssueAcceptQuest>(Handle_RequestBettingFraudIssueAcceptQuest);
        messageBroker.Subscribe<NetworkBettingFraudIssueQuestAccepted>(Handle_NetworkBettingFraudIssueQuestAccepted);
        messageBroker.Subscribe<NetworkBettingFraudIssueAcceptRejected>(Handle_NetworkBettingFraudIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BettingFraudIssueQuestAcceptTriggered>(Handle_BettingFraudIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestBettingFraudIssueAcceptQuest>(Handle_RequestBettingFraudIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkBettingFraudIssueQuestAccepted>(Handle_NetworkBettingFraudIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkBettingFraudIssueAcceptRejected>(Handle_NetworkBettingFraudIssueAcceptRejected);
    }

    private void Handle_BettingFraudIssueQuestAcceptTriggered(MessagePayload<BettingFraudIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.Thug, out var thugId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkBettingFraudIssueQuestAccepted(ownerId, hostControllerId, thugId));
        }
        else
        {
            network.SendAll(new RequestBettingFraudIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestBettingFraudIssueAcceptQuest(MessagePayload<RequestBettingFraudIssueAcceptQuest> payload)
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
                    nameof(RequestBettingFraudIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkBettingFraudIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is BettingFraudIssueBehavior.BettingFraudIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var thug) || !objectManager.TryGetIdWithLogging(thug, out var thugId))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkBettingFraudIssueQuestAccepted(ownerId, player.ControllerId, thugId));
            }
            else
            {
                network.Send(requester, new NetworkBettingFraudIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkBettingFraudIssueQuestAccepted(MessagePayload<NetworkBettingFraudIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.ThugId, out var thug)) return;

            issueInterface.MirrorQuestAccepted(owner, thug);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkBettingFraudIssueAcceptRejected(MessagePayload<NetworkBettingFraudIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
