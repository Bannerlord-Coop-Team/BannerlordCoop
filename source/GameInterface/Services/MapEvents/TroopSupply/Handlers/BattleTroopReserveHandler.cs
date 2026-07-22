using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply.Handlers;

/// <summary>
/// [Client] Receives each full reserve and initial-spawn entitlement, then feeds the matching side's troop supplier.
/// The supplier may not exist yet (the reserve can arrive before the mission builds), so this goes through
/// <see cref="CoopTroopSupplierRegistry"/>, which buffers until the supplier registers.
/// <para>
/// When the reserve carries <c>FlushRequested</c> (the BR-033 shrink refresh: a dropped owner returned and
/// this REPLACE takes its parties away from us), the server is waiting on our FINAL local pointers for the
/// dropped parties before it re-issues them to the returner — the ledger only has our last THROTTLED report
/// and may lag what we actually fielded. The pointers are captured atomically with the replace inside
/// <see cref="CoopTroopSupplier.SetReserve"/> (a dropped party can never be supplied again afterwards), so
/// acking AFTER applying still carries each party's definitive last word. One ack per flagged message —
/// sent even when nothing was dropped or the reserve was merely buffered — so the server can count acks
/// against its pending return.
/// </para>
/// </summary>
internal class BattleTroopReserveHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public BattleTroopReserveHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        messageBroker.Subscribe<NetworkBattleTroopReserve>(Handle_NetworkBattleTroopReserve);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleTroopReserve>(Handle_NetworkBattleTroopReserve);
    }

    private void Handle_NetworkBattleTroopReserve(MessagePayload<NetworkBattleTroopReserve> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;
        var dropped = CoopTroopSupplierRegistry.Feed(
            message.MapEventId,
            (BattleSideEnum)message.Side,
            message.Parties ?? Array.Empty<PartyReserve>(),
            message.GrantGeneration,
            message.CompletesInitialSizing);

        if (!message.FlushRequested)
            return;

        var entries = new SupplyProgressEntry[dropped.Count];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new SupplyProgressEntry(dropped[i].PartyId, dropped[i].Supplied);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkBattleSupplyProgress(
            message.MapEventId,
            entries,
            isFlush: true,
            grantGeneration: message.GrantGeneration));
    }
}
