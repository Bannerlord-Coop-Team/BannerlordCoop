using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Handlers;

internal class ShipPiecesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ShipPiecesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ShipPiecesChanged>(Handle_ShipPiecesChanged);
        messageBroker.Subscribe<NetworkShipPiecesChanged>(Handle_NetworkShipPiecesChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ShipPiecesChanged>(Handle_ShipPiecesChanged);
        messageBroker.Unsubscribe<NetworkShipPiecesChanged>(Handle_NetworkShipPiecesChanged);
    }

    private void Handle_ShipPiecesChanged(MessagePayload<ShipPiecesChanged> obj)
    {
        var ship = obj.What.Ship;

        if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) return;

        var slotTags = new string[ship._shipPieces.Count];
        var pieceHandles = new uint[ship._shipPieces.Count];

        var i = 0;
        foreach (var pair in ship._shipPieces)
        {
            slotTags[i] = pair.Key;

            if (pair.Value != null && !objectManager.TryGetHandleWithLogging(pair.Value, out pieceHandles[i])) return;

            i++;
        }

        network.SendAll(new NetworkShipPiecesChanged(shipHandle, slotTags, pieceHandles));
    }

    private void Handle_NetworkShipPiecesChanged(MessagePayload<NetworkShipPiecesChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return;

            var slotTags = data.SlotTags ?? Array.Empty<string>();
            var pieceHandles = data.PieceHandles ?? Array.Empty<uint>();
            if (slotTags.Length != pieceHandles.Length) return;

            var pieces = new ShipUpgradePiece[pieceHandles.Length];
            for (var i = 0; i < pieceHandles.Length; i++)
            {
                if (pieceHandles[i] == 0) continue;
                if (!objectManager.TryGetObjectWithLogging(pieceHandles[i], out pieces[i])) return;
            }

            // Rebuilt in the server's order because VersionNo hashes the slots in enumeration order
            ship._shipPieces.Clear();
            for (var i = 0; i < slotTags.Length; i++)
            {
                ship._shipPieces.Add(slotTags[i], pieces[i]);
            }

            ship.Owner?.MobileParty?.SetNavalVisualAsDirty();
            ship.UpdateVersionNo();
        });
    }
}
