using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.MapEvents;

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
    private readonly IBattleTroopReserveBuilder reserveBuilder;
    private readonly IObjectManager objectManager;

    public BattleSupplyProgressHandler(IMessageBroker messageBroker, IBattleTroopLedger ledger,
        IBattleTroopReserveBuilder reserveBuilder, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.ledger = ledger;
        this.reserveBuilder = reserveBuilder;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkBattleTroopHealth>(Handle_NetworkBattleTroopHealth);
        messageBroker.Subscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleTroopHealth>(Handle_NetworkBattleTroopHealth);
        messageBroker.Unsubscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
    }

    private void Handle_NetworkBattleTroopHealth(MessagePayload<NetworkBattleTroopHealth> payload)
    {
        if (ModInformation.IsClient) return;
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent) ||
                !objectManager.TryGetObjectWithLogging<MapEventParty>(message.PartyId, out var party)) return;
            if (party.Party?.MapEventSide?.MapEvent == mapEvent)
                reserveBuilder.RecordHealth(mapEvent, party, message.Survivors, message.SuppliedCount);
        }, context: nameof(Handle_NetworkBattleTroopHealth));
    }

    private void Handle_NetworkBattleSupplyProgress(MessagePayload<NetworkBattleSupplyProgress> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        if (message.Entries == null) return;

        foreach (var entry in message.Entries)
            ledger.ReportSupplied(message.MapEventId, entry.PartyId, entry.SuppliedCount);
    }
}
