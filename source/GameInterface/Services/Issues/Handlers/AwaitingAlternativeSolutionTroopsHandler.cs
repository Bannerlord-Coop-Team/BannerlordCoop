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
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using GameInterface.Services.UI.Messages;
using LiteNetLib;
using Serilog;
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
    private readonly IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry;

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager,
        IAwaitingAlternativeSolutionTroopsRegistry troopsRegistry)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;
        this.troopsRegistry = troopsRegistry;

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
        messageBroker.Subscribe<NetworkAwaitingAlternativeSolutionTroopsDrainResult>(Handle_DrainResult);
        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>(Handle_NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed);

        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
        messageBroker.Unsubscribe<NetworkAwaitingAlternativeSolutionTroopsDrainResult>(Handle_DrainResult);
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDepositedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDepositedLocally> payload)
    {
        if (ModInformation.IsServer) return;
        if (!objectManager.TryGetIdWithLogging(payload.What.IssueOwner, out var ownerId)) return;

        var packed = troopRosterInterface.PackTroopRosterData(payload.What.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        if (ModInformation.IsClient) return;
        GameThread.RunSafe(() =>
        {
            var requester = payload.Who as NetPeer;
            if (requester == null) return;

            // Only authoritative issue completion can turn dispatched troops into pending returns.
            network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDepositRejected(payload.What.OwnerId));
        });
    }

    private void Handle_NetworkAwaitingAlternativeSolutionTroopsDepositRejected(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDepositRejected> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider)) return;

            var localControllerId = controllerIdProvider.ControllerId;
            if (string.IsNullOrEmpty(localControllerId)) return;

            Logger.Error("Server rejected {Message} for owner {OwnerId} - rolling back the local speculative deposit",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), payload.What.OwnerId);
            troopsRegistry.Clear(localControllerId);
        });
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
        var requester = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (requester == null || !playerManager.TryGetPlayer(requester, out var player)) return;
            try
            {
                if (!troopsRegistry.TryGet(player.ControllerId, out var pending) ||
                    !objectManager.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero) ||
                    !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return;

                using (new MainHeroSubstitutionScope(hero, party))
                {
                    if (!Campaign.Current.Models.IssueModel.CanTroopsReturnFromAlternativeSolution()) return;
                    Campaign.Current.IssueManager.MakeAlternativeTroopsReturn(pending);
                    troopsRegistry.Clear(player.ControllerId);
                }
            }
            finally
            {
                var remaining = troopsRegistry.TryGet(player.ControllerId, out var pending)
                    ? troopRosterInterface.PackTroopRosterData(pending) : new TroopRosterData(System.Array.Empty<TroopRosterElementData>());
                network.Send(requester, new NetworkAwaitingAlternativeSolutionTroopsDrainResult(player.ControllerId, remaining));
            }
        });
    }

    private void Handle_DrainResult(MessagePayload<NetworkAwaitingAlternativeSolutionTroopsDrainResult> payload)
    {
        if (ModInformation.IsServer) return;
        GameThread.RunSafe(() =>
        {
            if (!ContainerProvider.TryResolve<IControllerIdProvider>(out var controller) ||
                controller.ControllerId != payload.What.OwnerControllerId) return;
            troopsRegistry.Clear(controller.ControllerId);
            troopsRegistry.Restore(controller.ControllerId, UnpackToRoster(payload.What.RemainingTroops));
            IssueManagerAlternativeSolutionTroopsPatches.CompleteReturnInquiry();
        });
    }

    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> payload) =>
        IssueManagerAlternativeSolutionTroopsPatches.CompleteReturnInquiry();

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
