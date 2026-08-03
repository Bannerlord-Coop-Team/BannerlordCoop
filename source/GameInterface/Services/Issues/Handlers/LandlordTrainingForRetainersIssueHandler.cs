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
/// Server-authoritative quest-solution-accept handling for Landlord Training for Retainers - own bespoke
/// messages/force-write (see <see cref="ILandlordTrainingForRetainersIssueInterface"/>'s doc comment for why),
/// same shape as <see cref="VillageNeedsCraftingMaterialsIssueHandler"/>'s equivalent. Creation rides the
/// shared <see cref="SimpleIssueCreationHandler"/>, alternative-solution-accept rides the fully generic mirror,
/// and removal rides the existing generic finalize choke point - none of those need a subscription here.
/// </summary>
internal class LandlordTrainingForRetainersIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LandlordTrainingForRetainersIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILandlordTrainingForRetainersIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public LandlordTrainingForRetainersIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILandlordTrainingForRetainersIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LandlordTrainingIssueQuestAcceptTriggered>(Handle_LandlordTrainingIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestLandlordTrainingIssueAcceptQuest>(Handle_RequestLandlordTrainingIssueAcceptQuest);
        messageBroker.Subscribe<NetworkLandlordTrainingIssueQuestAccepted>(Handle_NetworkLandlordTrainingIssueQuestAccepted);
        messageBroker.Subscribe<NetworkLandlordTrainingIssueAcceptRejected>(Handle_NetworkLandlordTrainingIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LandlordTrainingIssueQuestAcceptTriggered>(Handle_LandlordTrainingIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestLandlordTrainingIssueAcceptQuest>(Handle_RequestLandlordTrainingIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkLandlordTrainingIssueQuestAccepted>(Handle_NetworkLandlordTrainingIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkLandlordTrainingIssueAcceptRejected>(Handle_NetworkLandlordTrainingIssueAcceptRejected);
    }

    private void Handle_LandlordTrainingIssueQuestAcceptTriggered(MessagePayload<LandlordTrainingIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkLandlordTrainingIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.DifficultyMultiplier, payload.What.RewardGold));
        }
        else
        {
            network.SendAll(new RequestLandlordTrainingIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestLandlordTrainingIssueAcceptQuest(MessagePayload<RequestLandlordTrainingIssueAcceptQuest> payload)
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
                    nameof(RequestLandlordTrainingIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkLandlordTrainingIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var difficultyMultiplier, out var rewardGold))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkLandlordTrainingIssueQuestAccepted(ownerId, player.ControllerId, difficultyMultiplier, rewardGold));
            }
            else
            {
                network.Send(requester, new NetworkLandlordTrainingIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkLandlordTrainingIssueQuestAccepted(MessagePayload<NetworkLandlordTrainingIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.DifficultyMultiplier, data.RewardGold);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkLandlordTrainingIssueAcceptRejected(MessagePayload<NetworkLandlordTrainingIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            // RejectAcceptance is already fully generic (no type check at all) - reuse it instead of
            // duplicating the same rollback logic here.
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
