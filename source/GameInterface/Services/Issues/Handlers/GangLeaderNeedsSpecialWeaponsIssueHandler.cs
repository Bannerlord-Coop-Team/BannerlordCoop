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
/// Server-authoritative quest-solution-accept handling for Gang Leader Needs Special Weapons - own bespoke
/// messages/force-write (see <see cref="IGangLeaderNeedsSpecialWeaponsIssueInterface"/>'s doc comment).
/// Creation rides the shared <see cref="SimpleIssueCreationHandler"/> (the Issue ctor rolls nothing); this
/// issue type has no alternative solution (<c>IsThereAlternativeSolution == false</c>) and removal rides the
/// existing generic finalize choke point.
/// </summary>
internal class GangLeaderNeedsSpecialWeaponsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GangLeaderNeedsSpecialWeaponsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IGangLeaderNeedsSpecialWeaponsIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public GangLeaderNeedsSpecialWeaponsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IGangLeaderNeedsSpecialWeaponsIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<GangSpecialWeaponsIssueQuestAcceptTriggered>(Handle_GangSpecialWeaponsIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestGangSpecialWeaponsIssueAcceptQuest>(Handle_RequestGangSpecialWeaponsIssueAcceptQuest);
        messageBroker.Subscribe<NetworkGangSpecialWeaponsIssueQuestAccepted>(Handle_NetworkGangSpecialWeaponsIssueQuestAccepted);
        messageBroker.Subscribe<NetworkGangSpecialWeaponsIssueAcceptRejected>(Handle_NetworkGangSpecialWeaponsIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GangSpecialWeaponsIssueQuestAcceptTriggered>(Handle_GangSpecialWeaponsIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestGangSpecialWeaponsIssueAcceptQuest>(Handle_RequestGangSpecialWeaponsIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkGangSpecialWeaponsIssueQuestAccepted>(Handle_NetworkGangSpecialWeaponsIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkGangSpecialWeaponsIssueAcceptRejected>(Handle_NetworkGangSpecialWeaponsIssueAcceptRejected);
    }

    private void Handle_GangSpecialWeaponsIssueQuestAcceptTriggered(MessagePayload<GangSpecialWeaponsIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkGangSpecialWeaponsIssueQuestAccepted(ownerId, hostControllerId, payload.What.NumberOfDaggersRequested));
        }
        else
        {
            network.SendAll(new RequestGangSpecialWeaponsIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestGangSpecialWeaponsIssueAcceptQuest(MessagePayload<RequestGangSpecialWeaponsIssueAcceptQuest> payload)
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
                    nameof(RequestGangSpecialWeaponsIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkGangSpecialWeaponsIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var numberOfDaggersRequested))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkGangSpecialWeaponsIssueQuestAccepted(ownerId, player.ControllerId, numberOfDaggersRequested));
            }
            else
            {
                network.Send(requester, new NetworkGangSpecialWeaponsIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkGangSpecialWeaponsIssueQuestAccepted(MessagePayload<NetworkGangSpecialWeaponsIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.NumberOfDaggersRequested);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkGangSpecialWeaponsIssueAcceptRejected(MessagePayload<NetworkGangSpecialWeaponsIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
