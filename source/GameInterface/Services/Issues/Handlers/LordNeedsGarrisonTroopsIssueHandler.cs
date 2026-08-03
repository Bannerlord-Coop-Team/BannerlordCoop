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
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Server-authoritative creation AND quest-solution-accept handling for Lord Needs Garrison Troops - own
/// bespoke messages/force-write for both (see <see cref="ILordNeedsGarrisonTroopsIssueInterface"/>'s doc
/// comment: a creation-time roll the survey missed, on top of the accept-time live-model-read reward/count it
/// flagged). Alternative-solution-accept rides the fully generic mirror, and removal rides the existing
/// generic finalize choke point.
/// </summary>
internal class LordNeedsGarrisonTroopsIssueHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LordNeedsGarrisonTroopsIssueHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILordNeedsGarrisonTroopsIssueInterface issueInterface;
    private readonly IVillageNeedsToolsIssueInterface toolsIssueInterface;
    private readonly IPlayerManager playerManager;

    public LordNeedsGarrisonTroopsIssueHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ILordNeedsGarrisonTroopsIssueInterface issueInterface,
        IVillageNeedsToolsIssueInterface toolsIssueInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.toolsIssueInterface = toolsIssueInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<LordNeedsGarrisonTroopsIssueCreated>(Handle_LordNeedsGarrisonTroopsIssueCreated);
        messageBroker.Subscribe<NetworkLordNeedsGarrisonTroopsIssueCreated>(Handle_NetworkLordNeedsGarrisonTroopsIssueCreated);

        messageBroker.Subscribe<LordNeedsGarrisonTroopsIssueQuestAcceptTriggered>(Handle_LordNeedsGarrisonTroopsIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestLordNeedsGarrisonTroopsIssueAcceptQuest>(Handle_RequestLordNeedsGarrisonTroopsIssueAcceptQuest);
        messageBroker.Subscribe<NetworkLordNeedsGarrisonTroopsIssueQuestAccepted>(Handle_NetworkLordNeedsGarrisonTroopsIssueQuestAccepted);
        messageBroker.Subscribe<NetworkLordNeedsGarrisonTroopsIssueAcceptRejected>(Handle_NetworkLordNeedsGarrisonTroopsIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LordNeedsGarrisonTroopsIssueCreated>(Handle_LordNeedsGarrisonTroopsIssueCreated);
        messageBroker.Unsubscribe<NetworkLordNeedsGarrisonTroopsIssueCreated>(Handle_NetworkLordNeedsGarrisonTroopsIssueCreated);

        messageBroker.Unsubscribe<LordNeedsGarrisonTroopsIssueQuestAcceptTriggered>(Handle_LordNeedsGarrisonTroopsIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestLordNeedsGarrisonTroopsIssueAcceptQuest>(Handle_RequestLordNeedsGarrisonTroopsIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkLordNeedsGarrisonTroopsIssueQuestAccepted>(Handle_NetworkLordNeedsGarrisonTroopsIssueQuestAccepted);
        messageBroker.Unsubscribe<NetworkLordNeedsGarrisonTroopsIssueAcceptRejected>(Handle_NetworkLordNeedsGarrisonTroopsIssueAcceptRejected);
    }

    // --- Creation ---

    private void Handle_LordNeedsGarrisonTroopsIssueCreated(MessagePayload<LordNeedsGarrisonTroopsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;

        var issue = payload.What.Issue;
        if (issue?.IssueOwner == null) return;
        if (!objectManager.TryGetIdWithLogging(issue.IssueOwner, out var ownerId)) return;

        if (!issueInterface.TryCaptureFields(issue, out var settlement, out var neededTroopType))
        {
            Logger.Error("Could not capture Lord Needs Garrison Troops issue fields for owner {Owner}", ownerId);
            return;
        }

        if (!objectManager.TryGetIdWithLogging(settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(neededTroopType, out var neededTroopTypeId)) return;

        network.SendAll(new NetworkLordNeedsGarrisonTroopsIssueCreated(ownerId, settlementId, neededTroopTypeId));
    }

    private void Handle_NetworkLordNeedsGarrisonTroopsIssueCreated(MessagePayload<NetworkLordNeedsGarrisonTroopsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            if (owner.Issue != null) return; // idempotent

            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<CharacterObject>(data.NeededTroopTypeId, out var neededTroopType)) return;

            var replicated = issueInterface.ConstructReplicated(owner, settlement, neededTroopType);

            issueInterface.RegisterReplicated(owner, replicated);
        });
    }

    // --- Acceptance ---

    private void Handle_LordNeedsGarrisonTroopsIssueQuestAcceptTriggered(MessagePayload<LordNeedsGarrisonTroopsIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            VillageNeedsToolsIssueOwnership.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkLordNeedsGarrisonTroopsIssueQuestAccepted(
                ownerId, hostControllerId, payload.What.RequestedTroopAmount, payload.What.RewardGold));
        }
        else
        {
            network.SendAll(new RequestLordNeedsGarrisonTroopsIssueAcceptQuest(ownerId));
        }
    }

    private void Handle_RequestLordNeedsGarrisonTroopsIssueAcceptQuest(MessagePayload<RequestLordNeedsGarrisonTroopsIssueAcceptQuest> payload)
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
                    nameof(RequestLordNeedsGarrisonTroopsIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkLordNeedsGarrisonTroopsIssueAcceptRejected(ownerId));
                return;
            }

            if (owner.Issue is LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue && owner.Issue.IsOngoingWithoutQuest)
            {
                issueInterface.ReplayQuestAccepted(owner);
                if (!issueInterface.TryCaptureQuestFields(owner, out var requestedTroopAmount, out var rewardGold))
                {
                    Logger.Error("Replayed StartIssueQuest for owner {Owner} but could not read back its quest fields", ownerId);
                    return;
                }

                VillageNeedsToolsIssueOwnership.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkLordNeedsGarrisonTroopsIssueQuestAccepted(ownerId, player.ControllerId, requestedTroopAmount, rewardGold));
            }
            else
            {
                network.Send(requester, new NetworkLordNeedsGarrisonTroopsIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkLordNeedsGarrisonTroopsIssueQuestAccepted(MessagePayload<NetworkLordNeedsGarrisonTroopsIssueQuestAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner, data.RequestedTroopAmount, data.RewardGold);
            VillageNeedsToolsIssueOwnership.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void Handle_NetworkLordNeedsGarrisonTroopsIssueAcceptRejected(MessagePayload<NetworkLordNeedsGarrisonTroopsIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            toolsIssueInterface.RejectAcceptance(owner);
        });
    }
}
