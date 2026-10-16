using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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
    private readonly IIssueGenerationRegistry generationRegistry;
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;
    private readonly Dictionary<string, int> depositedGenerationByOwnerId = new();

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IIssueGenerationRegistry generationRegistry,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.generationRegistry = generationRegistry;
        this.troopsRegistry = troopsRegistry;

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDepositedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDepositedLocally> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.IssueOwner, out var ownerId)) return;

        if (ModInformation.IsServer)
        {
            if (!generationRegistry.TryGetGeneration(payload.What.IssueOwner, out var generation)) return;
            playerManager.TryGetPeer(payload.What.OwnerControllerId, out var peer);
            ConfirmDeposit(ownerId, payload.What.OwnerControllerId, generation, payload.What.Troops, peer);
            return;
        }

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(() => ApplyDepositRequest(payload));
    }

    private void ApplyDepositRequest(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        var requester = payload.Who as NetPeer;
        if (requester == null) return;

        // Only authoritative issue completion can turn dispatched troops into pending returns.
        network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(payload.What.OwnerId));
    }

    private void ConfirmDeposit(string ownerId, string controllerId, int generation, TroopRoster troops, NetPeer peer)
    {
        if (depositedGenerationByOwnerId.TryGetValue(ownerId, out var depositedGeneration) &&
            depositedGeneration == generation) return;

        troopsRegistry.Deposit(controllerId, troops);
        depositedGenerationByOwnerId[ownerId] = generation;
        if (peer == null) return;

        var packed = troopRosterInterface.PackTroopRosterData(troops);
        network.Send(peer, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed(ownerId, packed));
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

            troopsRegistry.Deposit(localControllerId, UnpackToRoster(payload.What.Troops));
        });
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDrainedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDrainedLocally> payload)
    {
        if (ModInformation.IsServer) return;

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDrain(packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDrain(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (ModInformation.IsClient) return;

        if (payload.Who is not NetPeer requester || !playerManager.TryGetPlayer(requester, out var player))
        {
            Logger.Error("Rejecting {Message} from an unregistered/unknown requester", nameof(RequestAwaitingAlternativeSolutionTroopsDrain));
            return;
        }

        GameThread.RunSafe(() => troopsRegistry.Withdraw(player.ControllerId, UnpackToRoster(payload.What.Troops)));
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
