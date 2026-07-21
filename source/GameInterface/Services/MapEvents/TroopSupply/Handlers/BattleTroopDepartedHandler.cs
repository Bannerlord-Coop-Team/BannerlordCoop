using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply.Messages;

namespace GameInterface.Services.MapEvents.TroopSupply.Handlers;

/// <summary>[Server] Retains exact routed troop identities for later reserve owners.</summary>
internal class BattleTroopDepartedHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IBattleTroopLedger ledger;

    public BattleTroopDepartedHandler(IMessageBroker messageBroker, IBattleTroopLedger ledger)
    {
        this.messageBroker = messageBroker;
        this.ledger = ledger;
        messageBroker.Subscribe<NetworkBattleTroopDeparted>(Handle_NetworkBattleTroopDeparted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleTroopDeparted>(Handle_NetworkBattleTroopDeparted);
    }

    private void Handle_NetworkBattleTroopDeparted(MessagePayload<NetworkBattleTroopDeparted> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        ledger.ReportDeparted(message.MapEventId, message.PartyId, message.TroopSeed);
    }
}
