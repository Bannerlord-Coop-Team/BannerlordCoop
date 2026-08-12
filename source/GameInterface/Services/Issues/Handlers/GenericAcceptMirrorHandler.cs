using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Interfaces;
using System;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

internal class GenericAcceptMirrorHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<GenericAcceptMirrorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IGenericAcceptMirrorInterface issueInterface;
    private readonly IPlayerManager playerManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPrisonerSaleValidator troopValidator;

    public GenericAcceptMirrorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IGenericAcceptMirrorInterface issueInterface,
        IPlayerManager playerManager,
        ITroopRosterInterface troopRosterInterface,
        IPrisonerSaleValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.issueInterface = issueInterface;
        this.playerManager = playerManager;
        this.troopRosterInterface = troopRosterInterface;
        this.troopValidator = troopValidator;

        messageBroker.Subscribe<GenericIssueQuestAcceptTriggered>(Handle_GenericIssueQuestAcceptTriggered);
        messageBroker.Subscribe<RequestGenericIssueAcceptQuest>(Handle_RequestGenericIssueAcceptQuest);
        messageBroker.Subscribe<NetworkGenericIssueQuestAccepted>(Handle_NetworkGenericIssueQuestAccepted);

        messageBroker.Subscribe<GenericIssueAlternativeAcceptTriggered>(Handle_GenericIssueAlternativeAcceptTriggered);
        messageBroker.Subscribe<RequestGenericIssueAcceptAlternative>(Handle_RequestGenericIssueAcceptAlternative);
        messageBroker.Subscribe<NetworkGenericIssueAlternativeAccepted>(Handle_NetworkGenericIssueAlternativeAccepted);

        messageBroker.Subscribe<NetworkGenericIssueAcceptRejected>(Handle_NetworkGenericIssueAcceptRejected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GenericIssueQuestAcceptTriggered>(Handle_GenericIssueQuestAcceptTriggered);
        messageBroker.Unsubscribe<RequestGenericIssueAcceptQuest>(Handle_RequestGenericIssueAcceptQuest);
        messageBroker.Unsubscribe<NetworkGenericIssueQuestAccepted>(Handle_NetworkGenericIssueQuestAccepted);

        messageBroker.Unsubscribe<GenericIssueAlternativeAcceptTriggered>(Handle_GenericIssueAlternativeAcceptTriggered);
        messageBroker.Unsubscribe<RequestGenericIssueAcceptAlternative>(Handle_RequestGenericIssueAcceptAlternative);
        messageBroker.Unsubscribe<NetworkGenericIssueAlternativeAccepted>(Handle_NetworkGenericIssueAlternativeAccepted);

        messageBroker.Unsubscribe<NetworkGenericIssueAcceptRejected>(Handle_NetworkGenericIssueAcceptRejected);
    }

    private void Handle_GenericIssueQuestAcceptTriggered(MessagePayload<GenericIssueQuestAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            network.SendAll(new NetworkGenericIssueQuestAccepted(ownerId, hostControllerId));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            network.SendAll(new RequestGenericIssueAcceptQuest(ownerId, generation));
        }
    }

    private void Handle_RequestGenericIssueAcceptQuest(MessagePayload<RequestGenericIssueAcceptQuest> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestGenericIssueAcceptQuest), ownerId);
                if (requester != null) network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestGenericIssueAcceptQuest), ownerId);
                network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest &&
                owner.Issue.IssueStayAliveConditions())
            {
                issueInterface.EnsureServerQuestMirror(owner);
                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                network.SendAll(new NetworkGenericIssueQuestAccepted(ownerId, player.ControllerId));
            }
            else
            {
                network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
            }
        });
    }

    private void Handle_NetworkGenericIssueQuestAccepted(MessagePayload<NetworkGenericIssueQuestAccepted> payload)
    {
        var ownerId = payload.What.OwnerId;
        var ownerControllerId = payload.What.OwnerControllerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.MirrorQuestAccepted(owner);
            IssueOwnershipRegistry.SetOwner(owner, ownerControllerId);
        });
    }

    private void Handle_GenericIssueAlternativeAcceptTriggered(MessagePayload<GenericIssueAlternativeAcceptTriggered> payload)
    {
        var owner = payload.What.Owner;
        if (owner == null || !objectManager.TryGetIdWithLogging(owner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            var hostControllerId = payload.What.ControllerId;
            if (hostControllerId == null || !playerManager.TryGetPlayer(hostControllerId, out var player)) return;

            var state = AlternativeSolutionStartRunner.StartOnServer(owner, player);
            IssueOwnershipRegistry.SetOwner(owner, hostControllerId);
            var hostTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new NetworkGenericIssueAlternativeAccepted(ownerId, hostControllerId, state, hostTroops));
        }
        else
        {
            IssueGenerationRegistry.TryGetGeneration(owner, out var generation);
            var packedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
            network.SendAll(new RequestGenericIssueAcceptAlternative(ownerId, generation, packedTroops));
        }
    }

    private void Handle_RequestGenericIssueAcceptAlternative(MessagePayload<RequestGenericIssueAcceptAlternative> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerId = payload.What.OwnerId;
        var requestedGeneration = payload.What.Generation;
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;

            if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
            {
                Logger.Error("Rejecting {Message} from an unregistered/unknown requester for owner {Owner}",
                    nameof(RequestGenericIssueAcceptAlternative), ownerId);
                if (requester != null) network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
                return;
            }

            if (!IssueGenerationRegistry.TryGetGeneration(owner, out var currentGeneration) || currentGeneration != requestedGeneration)
            {
                Logger.Error("Rejecting {Message} for a stale/superseded issue generation for owner {Owner}",
                    nameof(RequestGenericIssueAcceptAlternative), ownerId);
                network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
                return;
            }

            if (GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(owner.Issue) && owner.Issue.IsOngoingWithoutQuest &&
                owner.Issue.IssueStayAliveConditions())
            {
                AlternativeSolutionVanillaState state;
                try
                {
                    ApplyValidatedSentTroops(owner, player, payload.What.SentTroops);
                    state = AlternativeSolutionStartRunner.StartOnServer(owner, player);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Failed to apply {Message} for owner {Owner} - malformed or version-mismatched payload",
                        nameof(RequestGenericIssueAcceptAlternative), ownerId);
                    network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
                    return;
                }

                IssueOwnershipRegistry.SetOwner(owner, player.ControllerId);
                var validatedTroops = troopRosterInterface.PackTroopRosterData(owner.Issue.AlternativeSolutionSentTroops);
                network.SendAll(new NetworkGenericIssueAlternativeAccepted(ownerId, player.ControllerId, state, validatedTroops));
            }
            else
            {
                network.Send(requester, new NetworkGenericIssueAcceptRejected(ownerId));
            }
        });
    }

    private void ApplyValidatedSentTroops(Hero owner, Player player, TroopRosterData claimedTroops)
    {
        var claimedRoster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(claimedTroops))
        {
            claimedRoster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
        }

        var validatedRoster = player.MobilePartyId != null &&
            objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)
            ? troopValidator.Validate(claimedRoster, party.MemberRoster)
            : TroopRoster.CreateDummyTroopRoster();

        using (new AllowedThread())
        {
            owner.Issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in validatedRoster.GetTroopRoster())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(
                    element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            }
        }
    }

    private void Handle_NetworkGenericIssueAlternativeAccepted(MessagePayload<NetworkGenericIssueAlternativeAccepted> payload)
    {
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.OwnerId, out var owner) || owner.Issue == null) return;

            try
            {
                ApplyReceivedTroops(owner, data.SentTroops);
                issueInterface.MirrorAlternativeAccepted(owner, data.State);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to mirror {Message} for owner {Owner} - malformed or version-mismatched payload",
                    nameof(NetworkGenericIssueAlternativeAccepted), data.OwnerId);
                return;
            }

            IssueOwnershipRegistry.SetOwner(owner, data.OwnerControllerId);
        });
    }

    private void ApplyReceivedTroops(Hero owner, TroopRosterData troops)
    {
        using (new AllowedThread())
        {
            owner.Issue.AlternativeSolutionSentTroops.Clear();
            foreach (var element in troopRosterInterface.UnpackTroopRosterData(troops))
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(
                    element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
            }
        }
    }

    private void Handle_NetworkGenericIssueAcceptRejected(MessagePayload<NetworkGenericIssueAcceptRejected> payload)
    {
        var ownerId = payload.What.OwnerId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(ownerId, out var owner)) return;
            issueInterface.RejectAcceptance(owner);
        });
    }
}
