using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Handlers;

/// <summary>
/// Routes <see cref="Interfaces.AwaitingAlternativeSolutionTroopsRegistry"/> deposits/drains to the server.
/// Deliberately one-way (client -&gt; server only, no broadcast back to other clients): only the server's own
/// registry matters, since that is what a reconnecting owner's own client is restored from.
/// </summary>
internal class AwaitingAlternativeSolutionTroopsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly ITroopRosterInterface troopRosterInterface;

    public AwaitingAlternativeSolutionTroopsHandler(
        IMessageBroker messageBroker,
        INetwork network,
        ITroopRosterInterface troopRosterInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.troopRosterInterface = troopRosterInterface;

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

        AwaitingAlternativeSolutionTroopsRegistry.Clear(payload.What.OwnerControllerId);
    }
}
