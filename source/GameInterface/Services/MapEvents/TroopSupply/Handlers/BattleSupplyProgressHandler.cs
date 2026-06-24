using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply.Messages;

namespace GameInterface.Services.MapEvents.TroopSupply.Handlers;

/// <summary>
/// [Server] Applies clients' supply-progress reports to the authoritative <see cref="IBattleTroopLedger"/>,
/// advancing each party's pointer (monotonically). That pointer is what a new owner is resumed from on
/// disconnect/migration. The ledger is thread-safe, so this applies directly on the network thread.
/// </summary>
internal class BattleSupplyProgressHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IBattleTroopLedger ledger;

    public BattleSupplyProgressHandler(IMessageBroker messageBroker, IBattleTroopLedger ledger)
    {
        this.messageBroker = messageBroker;
        this.ledger = ledger;
        messageBroker.Subscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
    }

    private void Handle_NetworkBattleSupplyProgress(MessagePayload<NetworkBattleSupplyProgress> payload)
    {
        if (!ModInformation.IsServer) return;

        var message = payload.What;
        if (message.Entries == null) return;

        foreach (var entry in message.Entries)
            ledger.ReportSupplied(message.MapEventId, entry.PartyId, entry.SuppliedCount);
    }
}
