using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Data;
using GameInterface.Services.Ships.Messages;
using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// Replicates ship slot pieces and unlocked pieces from the server, and routes client equip requests to it.
/// </summary>
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
        messageBroker.Subscribe<ShipUpgradePieceEquipRequested>(Handle_ShipUpgradePieceEquipRequested);
        messageBroker.Subscribe<NetworkRequestEquipShipUpgradePiece>(Handle_NetworkRequestEquipShipUpgradePiece);
    }

    /// <summary>
    /// Unsubscribes every message this handler listens to.
    /// </summary>
    public void Dispose()
    {
        messageBroker.Unsubscribe<ShipPiecesChanged>(Handle_ShipPiecesChanged);
        messageBroker.Unsubscribe<NetworkShipPiecesChanged>(Handle_NetworkShipPiecesChanged);
        messageBroker.Unsubscribe<ShipUpgradePieceEquipRequested>(Handle_ShipUpgradePieceEquipRequested);
        messageBroker.Unsubscribe<NetworkRequestEquipShipUpgradePiece>(Handle_NetworkRequestEquipShipUpgradePiece);
    }

    /// <summary>
    /// Sends the server's full slot and unlocked piece snapshot of a ship to every client.
    /// </summary>
    private void Handle_ShipPiecesChanged(MessagePayload<ShipPiecesChanged> obj)
    {
        var ship = obj.What.Ship;

        if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) return;

        var slots = new ShipSlotPieceData[ship._shipPieces.Count];

        var i = 0;
        foreach (var pair in ship._shipPieces)
        {
            uint pieceHandle = 0;
            if (pair.Value != null && !objectManager.TryGetHandleWithLogging(pair.Value, out pieceHandle)) return;

            slots[i++] = new ShipSlotPieceData(pair.Key, pieceHandle);
        }

        uint[] unlockedPieceHandles = null;
        if (ship._unlockedUpgradePieces != null)
        {
            unlockedPieceHandles = new uint[ship._unlockedUpgradePieces.Count];
            for (var j = 0; j < unlockedPieceHandles.Length; j++)
            {
                if (!objectManager.TryGetHandleWithLogging(ship._unlockedUpgradePieces[j], out unlockedPieceHandles[j])) return;
            }
        }

        network.SendAll(new NetworkShipPiecesChanged(shipHandle, slots, unlockedPieceHandles));
    }

    /// <summary>
    /// Overwrites a client's ship slots and unlocked pieces with the server's snapshot.
    /// </summary>
    private void Handle_NetworkShipPiecesChanged(MessagePayload<NetworkShipPiecesChanged> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return;

            var slots = data.Slots ?? Array.Empty<ShipSlotPieceData>();

            var pieces = new ShipUpgradePiece[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i].PieceHandle == 0) continue;
                if (!objectManager.TryGetObjectWithLogging(slots[i].PieceHandle, out pieces[i])) return;
            }

            MBList<ShipUpgradePiece> unlockedPieces = null;
            if (data.HasUnlockedPieces)
            {
                var unlockedPieceHandles = data.UnlockedPieceHandles ?? Array.Empty<uint>();
                unlockedPieces = new MBList<ShipUpgradePiece>(unlockedPieceHandles.Length);
                foreach (var handle in unlockedPieceHandles)
                {
                    if (!objectManager.TryGetObjectWithLogging(handle, out ShipUpgradePiece piece)) return;
                    unlockedPieces.Add(piece);
                }
            }

            // Rebuilt in the server's order because VersionNo hashes the slots in enumeration order
            ship._shipPieces.Clear();
            for (var i = 0; i < slots.Length; i++)
            {
                ship._shipPieces.Add(slots[i].SlotTag, pieces[i]);
            }
            ship._unlockedUpgradePieces = unlockedPieces;

            ship.Owner?.MobileParty?.SetNavalVisualAsDirty();
            ship.UpdateVersionNo();
        });
    }

    /// <summary>
    /// Forwards a client's blocked equip to the server, which applies it and replicates the result.
    /// </summary>
    private void Handle_ShipUpgradePieceEquipRequested(MessagePayload<ShipUpgradePieceEquipRequested> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetHandleWithLogging(data.Ship, out var shipHandle)) return;

        uint pieceHandle = 0;
        if (data.Piece != null && !objectManager.TryGetHandleWithLogging(data.Piece, out pieceHandle)) return;

        network.SendAll(new NetworkRequestEquipShipUpgradePiece(shipHandle, data.SlotTag, pieceHandle));
    }

    /// <summary>
    /// Applies a client's equip request on the server with patches live so the pieces snapshot goes out.
    /// </summary>
    private void Handle_NetworkRequestEquipShipUpgradePiece(MessagePayload<NetworkRequestEquipShipUpgradePiece> obj)
    {
        if (ModInformation.IsClient) return;

        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return;

            ShipUpgradePiece piece = null;
            if (data.PieceHandle != 0 && !objectManager.TryGetObjectWithLogging(data.PieceHandle, out piece)) return;

            if (!ship.HasSlot(data.SlotTag)) return;

            ship.EquipUpgradePiece(data.SlotTag, piece);
        });
    }
}
