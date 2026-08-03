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
/// Server-authoritative quest-solution-accept handling for Gang Leader Needs Recruits - own bespoke
/// messages/force-write (see <see cref="IGangLeaderNeedsRecruitsIssueInterface"/>'s doc comment). Creation
/// rides the shared <see cref="SimpleIssueCreationHandler"/>, alternative-solution-accept rides the fully
/// generic mirror, and removal rides the existing generic finalize choke point.
/// </summary>
internal class GangLeaderNeedsRecruitsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsRecruitsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IGangLeaderNeedsRecruitsIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public GangLeaderNeedsRecruitsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IGangLeaderNeedsRecruitsIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<GangRecruitsIssueQuestAcceptTriggered>(Handle_GangRecruitsIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestGangRecruitsIssueAcceptQuest>(Handle_RequestGangRecruitsIssueAcceptQuest);
        messageBroker.Subscribe<NetworkGangRecruitsIssueQuestAccepted>(Handle_NetworkGangRecruitsIssueQuestAccepted);
        messageBroker.Subscribe<NetworkGangRecruitsIssueAcceptRejected>(Handle_NetworkGangRecruitsIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GangRecruitsIssueQuestAcceptTriggered>(Handle_GangRecruitsIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestGangRecruitsIssueAcceptQuest>(Handle_RequestGangRecruitsIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkGangRecruitsIssueQuestAccepted>(Handle_NetworkGangRecruitsIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkGangRecruitsIssueAcceptRejected>(Handle_NetworkGangRecruitsIssueAcceptRejected);
    }

    private void Handle_GangRecruitsIssueQuestAcceptTriggered(MessagePayload<GangRecruitsIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkGangRecruitsIssueQuestAccepted(ownerId, hostControllerId, payload.What.RequestedRecruitCount));
        }
        else
        {
            network.SendAll(new RequestGangRecruitsIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestGangRecruitsIssueAcceptQuest(MessagePayload<RequestGangRecruitsIssueAcceptQuest> payload)
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
                    nameof(RequestGangRecruitsIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkGangRecruitsIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var requestedRecruitCount))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkGangRecruitsIssueQuestAccepted(ownerId, player.ControllerId, requestedRecruitCount));
            }
            else
            {
                network.Send(requester, new NetworkGangRecruitsIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkGangRecruitsIssueQuestAccepted(MessagePayload<NetworkGangRecruitsIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.RequestedRecruitCount);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkGangRecruitsIssueAcceptRejected(MessagePayload<NetworkGangRecruitsIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
