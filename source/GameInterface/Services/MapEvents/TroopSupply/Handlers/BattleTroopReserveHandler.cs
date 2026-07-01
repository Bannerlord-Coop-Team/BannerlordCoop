using Common;
using Common.Messaging;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.TroopSupply.Handlers;

/// <summary>
/// [Client] Receives the server's per-party reserve and feeds it into the matching side's troop supplier.
/// The supplier may not exist yet (the reserve can arrive before the mission builds), so this goes through
/// <see cref="CoopTroopSupplierRegistry"/>, which buffers until the supplier registers.
/// </summary>
internal class BattleTroopReserveHandler : IHandler
{
    private readonly IMessageBroker messageBroker;

    public BattleTroopReserveHandler(IMessageBroker messageBroker)
    {
        this.messageBroker = messageBroker;
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
        CoopTroopSupplierRegistry.Feed(
            message.MapEventId,
            (BattleSideEnum)message.Side,
            message.Parties ?? Array.Empty<PartyReserve>());
    }
}
