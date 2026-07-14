using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.UI.Handlers;

internal class TacticalUnitSymbolsOptionsHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public TacticalUnitSymbolsOptionsHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<TacticalUnitSymbolsVisibilitySelected>(Handle_TacticalUnitSymbolsVisibilitySelected);
        messageBroker.Subscribe<NetworkRequestTacticalUnitSymbolsVisibilityChange>(Handle_NetworkRequestTacticalUnitSymbolsVisibilityChange);
        messageBroker.Subscribe<NetworkTacticalUnitSymbolsVisibilityChanged>(Handle_NetworkTacticalUnitSymbolsVisibilityChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TacticalUnitSymbolsVisibilitySelected>(Handle_TacticalUnitSymbolsVisibilitySelected);
        messageBroker.Unsubscribe<NetworkRequestTacticalUnitSymbolsVisibilityChange>(Handle_NetworkRequestTacticalUnitSymbolsVisibilityChange);
        messageBroker.Unsubscribe<NetworkTacticalUnitSymbolsVisibilityChanged>(Handle_NetworkTacticalUnitSymbolsVisibilityChanged);
    }

    private void Handle_TacticalUnitSymbolsVisibilitySelected(MessagePayload<TacticalUnitSymbolsVisibilitySelected> payload)
    {
        if (ModInformation.IsServer)
        {
            SetAndBroadcast(payload.What.HideTacticalUnitSymbols);
            return;
        }

        network.SendAll(new NetworkRequestTacticalUnitSymbolsVisibilityChange(payload.What.HideTacticalUnitSymbols));
    }

    private void Handle_NetworkRequestTacticalUnitSymbolsVisibilityChange(MessagePayload<NetworkRequestTacticalUnitSymbolsVisibilityChange> payload)
    {
        if (ModInformation.IsClient) return;

        SetAndBroadcast(payload.What.HideTacticalUnitSymbols);
    }

    private void Handle_NetworkTacticalUnitSymbolsVisibilityChanged(MessagePayload<NetworkTacticalUnitSymbolsVisibilityChanged> payload)
    {
        if (ModInformation.IsServer) return;

        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(payload.What.HideTacticalUnitSymbols);
    }

    internal void SetAndBroadcast(bool hideTacticalUnitSymbols)
    {
        TacticalUnitSymbolsSettings.SetHideTacticalUnitSymbols(hideTacticalUnitSymbols);
        network.SendAll(new NetworkTacticalUnitSymbolsVisibilityChanged(hideTacticalUnitSymbols));
    }
}
