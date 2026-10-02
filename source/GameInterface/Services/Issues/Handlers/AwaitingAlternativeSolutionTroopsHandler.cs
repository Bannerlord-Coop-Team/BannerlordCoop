using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

internal class AwaitingAlternativeSolutionTroopsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AwaitingAlternativeSolutionTroopsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;
    private readonly IIssueOwnershipRegistry ownershipRegistry;
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;
    private readonly IPrisonerSaleValidator troopValidator;
    private readonly Dictionary<string, int> depositedGenerationByOwnerId = new();

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IIssueOwnershipRegistry ownershipRegistry,
        IIssueGenerationRegistry generationRegistry,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry,
        IPrisonerSaleValidator troopValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.ownershipRegistry = ownershipRegistry;
        this.generationRegistry = generationRegistry;
        this.troopsRegistry = troopsRegistry;
        this.troopValidator = troopValidator;
        IssueManagerAlternativeSolutionTroopsPatches.ResetReturnInquiry();

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Subscribe<AlternativeSolutionTroopsReturnRequested>(HandleReturnRequested);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Unsubscribe<AlternativeSolutionTroopsReturnRequested>(HandleReturnRequested);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDepositedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDepositedLocally> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.IssueOwner, out var ownerId)) return;
        if (ModInformation.IsServer)
        {
            if (playerManager.TryGetPeer(payload.What.OwnerControllerId, out var peer))
                SendPendingTroops(peer, payload.What.OwnerControllerId, ownerId);
            return;
        }

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() => ApplyDeposit(payload));
    }

    private void ApplyDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        var requester = payload.Who as NetPeer;
        if (requester == null || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit));
            if (requester != null) network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(payload.What.OwnerId));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Hero>(payload.What.OwnerId, out var owner))
        {
            Logger.Error("Rejecting {Message} for an unknown owner {OwnerId}", nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            RejectDeposit(requester, player.ControllerId, payload.What.OwnerId);
            return;
        }

        if (!ownershipRegistry.TryGetOwnerControllerId(owner, out var recordedOwner) || recordedOwner != player.ControllerId)
        {
            Logger.Error("Rejecting {Message} from {Requester}, who is not the recorded owner of {Owner}",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), player.ControllerId, payload.What.OwnerId);
            RejectDeposit(requester, player.ControllerId, payload.What.OwnerId);
            return;
        }

        if (owner.Issue is not { IsSolvingWithAlternative: true } issue)
        {
            Logger.Error("Rejecting {Message} for {Owner}, whose issue is not solving with an alternative solution",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            RejectDeposit(requester, player.ControllerId, payload.What.OwnerId);
            return;
        }

        if (!generationRegistry.TryGetGeneration(owner, out var currentGeneration))
        {
            Logger.Error("Rejecting {Message} for {Owner} - no tracked issue generation",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            RejectDeposit(requester, player.ControllerId, payload.What.OwnerId);
            return;
        }

        if (depositedGenerationByOwnerId.TryGetValue(payload.What.OwnerId, out var lastDepositedGeneration)
            && lastDepositedGeneration == currentGeneration)
        {
            SendPendingTroops(requester, player.ControllerId, payload.What.OwnerId);
            return;
        }

        var claimedRoster = UnpackToRoster(payload.What.Troops);
        var validatedRoster = troopValidator.Validate(claimedRoster, issue.AlternativeSolutionSentTroops, preserveTroopXp: true);
        depositedGenerationByOwnerId[payload.What.OwnerId] = currentGeneration;
        troopsRegistry.Deposit(player.ControllerId, validatedRoster);

        SendPendingTroops(requester, player.ControllerId, payload.What.OwnerId);
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDepositRejected> payload)
    {
        if (ModInformation.IsServer) return;
        Logger.Error("Server rejected {Message} for owner {OwnerId}",
            nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;
            var localControllerId = controllerIdProvider.ControllerId;
            if (string.IsNullOrEmpty(localControllerId)) return;
            // Confirmations replace the whole pending balance, including after a rejected return.
            troopsRegistry.Clear(localControllerId);
            troopsRegistry.Restore(localControllerId, UnpackToRoster(payload.What.Troops));
            if (payload.What.ReturnResponse) IssueManagerAlternativeSolutionTroopsPatches.ResetReturnInquiry();
        });
    }

    private void HandleReturnRequested(MessagePayload<AlternativeSolutionTroopsReturnRequested> payload)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDrain());
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDrain(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() => ApplyReturn(payload));
    }

    private void ApplyReturn(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (payload.Who is not NetPeer requester || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDrain));
            return;
        }

        try
        {
            if (!troopsRegistry.TryGet(player.ControllerId, out var troops)) return;
            if (!objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;
            using (new MainHeroSubstitutionScope(hero, party))
            {
                if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;
                Campaign.Current.IssueManager.MakeAlternativeTroopsReturn(troops);
            }
            troopsRegistry.Clear(player.ControllerId);
        }
        finally
        {
            SendPendingTroops(requester, player.ControllerId, null, returnResponse: true);
        }
    }

    private void RejectDeposit(NetPeer requester, string controllerId, string ownerId)
    {
        network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(ownerId));
        SendPendingTroops(requester, controllerId, ownerId);
    }

    private void SendPendingTroops(NetPeer requester, string controllerId, string ownerId, bool returnResponse = false)
    {
        if (!troopsRegistry.TryGet(controllerId, out var pending)) pending = TroopRoster.CreateDummyTroopRoster();
        network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(ownerId,
            troopRosterInterface.PackTroopRosterData(pending), returnResponse));
    }

    private TroopRoster UnpackToRoster(TroopRosterData troops)
    {
        var roster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(troops))
        {
            int index = roster.AddNewElement(element.Character, -1);
            roster.data[index] = element;
        }
        roster.UpdateVersion();
        roster.InitializeCachedData();

        return roster;
    }
}
