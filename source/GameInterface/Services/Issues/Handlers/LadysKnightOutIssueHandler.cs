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
/// Server-authoritative quest-solution-accept handling for Lady's Knight Out - own bespoke messages/force-write
/// (see <see cref="ILadysKnightOutIssueInterface"/>'s doc comment). Creation rides the shared
/// <see cref="SimpleIssueCreationHandler"/>, removal rides the existing generic finalize choke point. No
/// alternative-solution handling (<c>IsThereAlternativeSolution == false</c>); the Lord Solution path is
/// disabled entirely (see <see cref="Patches.LadysKnightOutLordSolutionDisablePatch"/>), so it needs no
/// handling here either.
/// </summary>
internal class LadysKnightOutIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LadysKnightOutIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILadysKnightOutIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public LadysKnightOutIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILadysKnightOutIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LadysKnightOutIssueQuestAcceptTriggered>(Handle_LadysKnightOutIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestLadysKnightOutIssueAcceptQuest>(Handle_RequestLadysKnightOutIssueAcceptQuest);
        messageBroker.Subscribe<NetworkLadysKnightOutIssueQuestAccepted>(Handle_NetworkLadysKnightOutIssueQuestAccepted);
        messageBroker.Subscribe<NetworkLadysKnightOutIssueAcceptRejected>(Handle_NetworkLadysKnightOutIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LadysKnightOutIssueQuestAcceptTriggered>(Handle_LadysKnightOutIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestLadysKnightOutIssueAcceptQuest>(Handle_RequestLadysKnightOutIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkLadysKnightOutIssueQuestAccepted>(Handle_NetworkLadysKnightOutIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkLadysKnightOutIssueAcceptRejected>(Handle_NetworkLadysKnightOutIssueAcceptRejected);
    }

    private void Handle_LadysKnightOutIssueQuestAcceptTriggered(MessagePayload<LadysKnightOutIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkLadysKnightOutIssueQuestAccepted(ownerId, hostControllerId, payload.What.DifficultyMultiplier));
        }
        else
        {
            network.SendAll(new RequestLadysKnightOutIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestLadysKnightOutIssueAcceptQuest(MessagePayload<RequestLadysKnightOutIssueAcceptQuest> payload)
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
                    nameof(RequestLadysKnightOutIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkLadysKnightOutIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is LadysKnightOutIssueBehavior.LadysKnightOutIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var difficultyMultiplier))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkLadysKnightOutIssueQuestAccepted(ownerId, player.ControllerId, difficultyMultiplier));
            }
            else
            {
                network.Send(requester, new NetworkLadysKnightOutIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkLadysKnightOutIssueQuestAccepted(MessagePayload<NetworkLadysKnightOutIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.DifficultyMultiplier);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkLadysKnightOutIssueAcceptRejected(MessagePayload<NetworkLadysKnightOutIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
