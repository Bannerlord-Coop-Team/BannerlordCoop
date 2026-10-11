using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Ships.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Ships.Handlers;

/// <summary>
/// Replicates ship upgrade slots, figureheads and unlocked upgrade pieces from the server.
/// </summary>
internal class ShipUpgradeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ShipUpgradeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ShipUpgradeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<ShipUpgradePieceChanged>(Handle_ShipUpgradePieceChanged);
        messageBroker.Subscribe<NetworkSetShipUpgradePiece>(Handle_NetworkSetShipUpgradePiece);
        messageBroker.Subscribe<ShipFigureheadChanged>(Handle_ShipFigureheadChanged);
        messageBroker.Subscribe<NetworkChangeShipFigurehead>(Handle_NetworkChangeShipFigurehead);
        messageBroker.Subscribe<ShipUnlockedUpgradePiecesChanged>(Handle_ShipUnlockedUpgradePiecesChanged);
        messageBroker.Subscribe<NetworkSetShipUnlockedUpgradePieces>(Handle_NetworkSetShipUnlockedUpgradePieces);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ShipUpgradePieceChanged>(Handle_ShipUpgradePieceChanged);
        messageBroker.Unsubscribe<NetworkSetShipUpgradePiece>(Handle_NetworkSetShipUpgradePiece);
        messageBroker.Unsubscribe<ShipFigureheadChanged>(Handle_ShipFigureheadChanged);
        messageBroker.Unsubscribe<NetworkChangeShipFigurehead>(Handle_NetworkChangeShipFigurehead);
        messageBroker.Unsubscribe<ShipUnlockedUpgradePiecesChanged>(Handle_ShipUnlockedUpgradePiecesChanged);
        messageBroker.Unsubscribe<NetworkSetShipUnlockedUpgradePieces>(Handle_NetworkSetShipUnlockedUpgradePieces);
    }

    private void Handle_ShipUpgradePieceChanged(MessagePayload<ShipUpgradePieceChanged> payload)
    {
        var change = payload.What;
        if (!objectManager.TryGetHandleWithLogging(change.Ship, out var shipHandle)) return;

        uint pieceHandle = 0;
        if (change.Piece != null && !objectManager.TryGetHandleWithLogging(change.Piece, out pieceHandle)) return;

        network.SendAll(new NetworkSetShipUpgradePiece(shipHandle, change.SlotTag, pieceHandle));
    }

    // Sets the slot only; the server's hit point rescale arrives through the synced hit point fields.
    private void Handle_NetworkSetShipUpgradePiece(MessagePayload<NetworkSetShipUpgradePiece> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(message.ShipHandle, out var ship)) return;

            ShipUpgradePiece piece = null;
            if (message.PieceHandle != 0 && !objectManager.TryGetObjectWithLogging(message.PieceHandle, out piece)) return;

            if (!ship._shipPieces.ContainsKey(message.SlotTag))
            {
                Logger.Error("Ship {ShipHandle} has no upgrade slot {SlotTag}", message.ShipHandle, message.SlotTag);
                return;
            }

            using (new AllowedThread())
            {
                ship._shipPieces[message.SlotTag] = piece;
                ship.UpdateVersionNo();
                ship.Owner?.MobileParty?.SetNavalVisualAsDirty();
            }
        }, context: nameof(NetworkSetShipUpgradePiece));
    }

    private void Handle_ShipFigureheadChanged(MessagePayload<ShipFigureheadChanged> payload)
    {
        var ship = payload.What.Ship;
        if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) return;

        uint figureheadHandle = 0;
        if (ship.Figurehead != null && !objectManager.TryGetHandleWithLogging(ship.Figurehead, out figureheadHandle)) return;

        network.SendAll(new NetworkChangeShipFigurehead(shipHandle, figureheadHandle));
    }

    private void Handle_NetworkChangeShipFigurehead(MessagePayload<NetworkChangeShipFigurehead> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(message.ShipHandle, out var ship)) return;

            Figurehead figurehead = null;
            if (message.FigureheadHandle != 0 && !objectManager.TryGetObjectWithLogging(message.FigureheadHandle, out figurehead)) return;

            using (new AllowedThread())
            {
                ship.ChangeFigurehead(figurehead);
            }
        }, context: nameof(NetworkChangeShipFigurehead));
    }

    private void Handle_ShipUnlockedUpgradePiecesChanged(MessagePayload<ShipUnlockedUpgradePiecesChanged> payload)
    {
        var ship = payload.What.Ship;
        if (!objectManager.TryGetHandleWithLogging(ship, out var shipHandle)) return;

        var pieces = ship._unlockedUpgradePieces;
        if (pieces == null)
        {
            network.SendAll(new NetworkSetShipUnlockedUpgradePieces(shipHandle, isNull: true, Array.Empty<uint>()));
            return;
        }

        var pieceHandles = new uint[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            if (!objectManager.TryGetHandleWithLogging(pieces[i], out pieceHandles[i])) return;
        }

        network.SendAll(new NetworkSetShipUnlockedUpgradePieces(shipHandle, isNull: false, pieceHandles));
    }

    private void Handle_NetworkSetShipUnlockedUpgradePieces(MessagePayload<NetworkSetShipUnlockedUpgradePieces> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Ship>(message.ShipHandle, out var ship)) return;

            if (message.IsNull)
            {
                ship._unlockedUpgradePieces = null;
                return;
            }

            var pieceHandles = message.PieceHandles ?? Array.Empty<uint>();
            var pieces = new MBList<ShipUpgradePiece>(pieceHandles.Length);
            foreach (var pieceHandle in pieceHandles)
            {
                if (!objectManager.TryGetObjectWithLogging(pieceHandle, out ShipUpgradePiece piece)) return;
                pieces.Add(piece);
            }

            ship._unlockedUpgradePieces = pieces;
        }, context: nameof(NetworkSetShipUnlockedUpgradePieces));
    }
}
