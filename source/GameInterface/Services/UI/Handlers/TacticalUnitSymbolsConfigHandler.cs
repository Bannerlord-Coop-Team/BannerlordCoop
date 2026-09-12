using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.UI.Messages;

namespace GameInterface.Services.UI.Handlers;

internal class TacticalUnitSymbolsConfigHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public TacticalUnitSymbolsConfigHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        messageBroker.Subscribe<NetworkTacticalUnitSymbolsVisibilityChanged>(Handle_NetworkTacticalUnitSymbolsVisibilityChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkTacticalUnitSymbolsVisibilityChanged>(Handle_NetworkTacticalUnitSymbolsVisibilityChanged);
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
