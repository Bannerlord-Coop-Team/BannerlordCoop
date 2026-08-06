using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.TroopRosters.Interfaces;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

internal class AwaitingAlternativeSolutionTroopsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AwaitingAlternativeSolutionTroopsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;
    private readonly IPlayerManager playerManager;

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ITroopRosterInterface troopRosterInterface,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;
        this.playerManager = playerManager;

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);

        messageBroker.Subscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Subscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDepositedLocally>(Handle_AwaitingAlternativeSolutionTroopsDepositedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDeposit>(Handle_RequestAwaitingAlternativeSolutionTroopsDeposit);

        messageBroker.Unsubscribe<AwaitingAlternativeSolutionTroopsDrainedLocally>(Handle_AwaitingAlternativeSolutionTroopsDrainedLocally);
        messageBroker.Unsubscribe<RequestAwaitingAlternativeSolutionTroopsDrain>(Handle_RequestAwaitingAlternativeSolutionTroopsDrain);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDepositedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDepositedLocally> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;
        var packed = troopRosterInterface.PackTroopRosterData(data.Troops);
        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(data.OwnerControllerId, packed));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDeposit(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDeposit> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        if (!IsFromRegisteredPeer(payload.Who) || !playerManager.TryGetPlayer(data.OwnerControllerId, out _))
        {
            Logger.Error("Rejecting {Message} for unrecognized/unregistered owner {Owner}",
                nameof(RequestAwaitingAlternativeSolutionTroopsDeposit), data.OwnerControllerId);
            return;
        }

        var roster = TroopRoster.CreateDummyTroopRoster();
        foreach (var element in troopRosterInterface.UnpackTroopRosterData(data.Troops))
        {
            roster.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp, false);
        }

        AwaitingAlternativeSolutionTroopsRegistry.Deposit(data.OwnerControllerId, roster);
    }

    private void Handle_AwaitingAlternativeSolutionTroopsDrainedLocally(MessagePayload<AwaitingAlternativeSolutionTroopsDrainedLocally> payload)
    {
        if (ModInformation.IsServer) return;

        network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDrain(payload.What.OwnerControllerId));
    }

    private void Handle_RequestAwaitingAlternativeSolutionTroopsDrain(MessagePayload<RequestAwaitingAlternativeSolutionTroopsDrain> payload)
    {
        if (ModInformation.IsClient) return;

        var ownerControllerId = payload.What.OwnerControllerId;
        if (!IsFromRegisteredPeer(payload.Who) || !playerManager.TryGetPlayer(ownerControllerId, out _))
        {
            Logger.Error("Rejecting {Message} for unrecognized/unregistered owner {Owner}",
                nameof(RequestAwaitingAlternativeSolutionTroopsDrain), ownerControllerId);
            return;
        }

        AwaitingAlternativeSolutionTroopsRegistry.Clear(ownerControllerId);
    }

    private bool IsFromRegisteredPeer(object who) => who is NetPeer peer && playerManager.TryGetPlayer(peer, out _);
}
