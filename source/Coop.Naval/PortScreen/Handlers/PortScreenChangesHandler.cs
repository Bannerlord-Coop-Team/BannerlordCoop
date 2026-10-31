using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Interfaces;
using Coop.Naval.PortScreen.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Naval.PortScreen.Handlers;

internal class PortScreenChangesHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPortScreenHandlerInterface portScreenHandlerInterface;

    public PortScreenChangesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPortScreenHandlerInterface portScreenHandlerInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.portScreenHandlerInterface = portScreenHandlerInterface;

        messageBroker.Subscribe<PortScreenChangesRequested>(Handle_PortScreenChangesRequested);
        messageBroker.Subscribe<NetworkPortScreenChangesRequested>(Handle_NetworkPortScreenChangesRequested);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PortScreenChangesRequested>(Handle_PortScreenChangesRequested);
        messageBroker.Unsubscribe<NetworkPortScreenChangesRequested>(Handle_NetworkPortScreenChangesRequested);
    }

    private void Handle_PortScreenChangesRequested(MessagePayload<PortScreenChangesRequested> obj)
    {
        var data = obj.What;

        // Missing sides are sent as handle 0
        uint rightOwnerHandle = 0;
        uint leftOwnerHandle = 0;
        if (data.RightOwner != null && !objectManager.TryGetHandleWithLogging(data.RightOwner, out rightOwnerHandle)) return;
        if (data.LeftOwner != null && !objectManager.TryGetHandleWithLogging(data.LeftOwner, out leftOwnerHandle)) return;

        if (!objectManager.TryGetHandleWithLogging(data.MainHero, out var mainHeroHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(data.PlayerClan, out var playerClanHandle)) return;
        if (!objectManager.TryGetHandleWithLogging(data.MainParty, out var mainPartyHandle)) return;

        if (!TryConvertList<Ship, uint>(data.ShipsToBuy, objectManager.TryGetHandleWithLogging, out var packedShipsToBuy)) return;
        if (!TryConvertList<Ship, uint>(data.ShipsToSell, objectManager.TryGetHandleWithLogging, out var packedShipsToSell)) return;
        if (!TryConvertList<Ship, uint>(data.ShipsToRepair, objectManager.TryGetHandleWithLogging, out var packedShipsToRepair)) return;
        if (!TryConvertList<ShipRenameInfo, ShipRenameData>(data.ShipsToRename, TryPackChangeInfo, out var packedShipsToRename)) return;
        if (!TryConvertList<ShipUpgradePieceInfo, ShipUpgradePieceData>(data.SelectedShipPieces, TryPackChangeInfo, out var packedSelectedShipPieces)) return;
        if (!TryConvertList<ShipFigureheadInfo, ShipFigureheadData>(data.SelectedFigureheads, TryPackChangeInfo, out var packedSelectedFigureheads)) return;
        if (!TryConvertList<Ship, uint>(data.ShipsToSend, objectManager.TryGetHandleWithLogging, out var packedShipsToSend)) return;

        var message = new NetworkPortScreenChangesRequested(
            data.Mode,
            rightOwnerHandle,
            leftOwnerHandle,
            mainHeroHandle,
            playerClanHandle,
            mainPartyHandle,
            packedShipsToBuy,
            packedShipsToSell,
            packedShipsToRepair,
            packedShipsToRename,
            packedSelectedShipPieces,
            packedSelectedFigureheads,
            packedShipsToSend);

        network.SendAll(message);
    }

    private void Handle_NetworkPortScreenChangesRequested(MessagePayload<NetworkPortScreenChangesRequested> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            PartyBase rightOwner = null;
            PartyBase leftOwner = null;
            if (data.RightOwnerHandle != 0 && !objectManager.TryGetObjectWithLogging(data.RightOwnerHandle, out rightOwner)) return;
            if (data.LeftOwnerHandle != 0 && !objectManager.TryGetObjectWithLogging(data.LeftOwnerHandle, out leftOwner)) return;

            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroHandle, out var mainHero)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(data.PlayerClanHandle, out var playerClan)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(data.MainPartyHandle, out var mainParty)) return;

            if (!TryConvertList<uint, Ship>(data.PackedShipsToBuy, objectManager.TryGetObjectWithLogging, out var shipsToBuy)) return;
            if (!TryConvertList<uint, Ship>(data.PackedShipsToSell, objectManager.TryGetObjectWithLogging, out var shipsToSell)) return;
            if (!TryConvertList<uint, Ship>(data.PackedShipsToRepair, objectManager.TryGetObjectWithLogging, out var shipsToRepair)) return;
            if (!TryConvertList<ShipRenameData, ShipRenameInfo>(data.PackedShipsToRename, TryUnpackChangeInfo, out var shipsToRename)) return;
            if (!TryConvertList<ShipUpgradePieceData, ShipUpgradePieceInfo>(data.PackedSelectedShipPieces, TryUnpackChangeInfo, out var selectedShipPieces)) return;
            if (!TryConvertList<ShipFigureheadData, ShipFigureheadInfo>(data.PackedSelectedFigureheads, TryUnpackChangeInfo, out var selectedFigureheads)) return;
            if (!TryConvertList<uint, Ship>(data.PackedShipsToSend, objectManager.TryGetObjectWithLogging, out var shipsToSend)) return;

            var changes = new PortScreenChanges(
                rightOwner,
                leftOwner,
                mainHero,
                playerClan,
                mainParty,
                shipsToBuy,
                shipsToSell,
                shipsToRepair,
                shipsToRename,
                selectedShipPieces,
                selectedFigureheads,
                shipsToSend);

            portScreenHandlerInterface.OnConfirmChanges(data.Mode, changes);
        });
    }

    private delegate bool TryConvert<TSource, TResult>(TSource source, out TResult result);

    private static bool TryConvertList<TSource, TResult>(
        MBReadOnlyList<TSource> sourceList,
        TryConvert<TSource, TResult> tryConvert,
        out MBReadOnlyList<TResult> convertedList)
    {
        convertedList = new MBReadOnlyList<TResult>();

        // Protobuf doesn't send empty lists, so they arrive as null
        if (sourceList == null) return true;

        foreach (var source in sourceList)
        {
            if (!tryConvert(source, out var result)) return false;
            convertedList.Add(result);
        }

        return true;
    }

    private bool TryPackChangeInfo(ShipRenameInfo info, out ShipRenameData data)
    {
        data = new();
        if (!objectManager.TryGetHandleWithLogging(info.Ship, out var shipHandle)) return false;

        data = new(shipHandle, info.NewName);
        return true;
    }

    private bool TryPackChangeInfo(ShipUpgradePieceInfo info, out ShipUpgradePieceData data)
    {
        data = new();
        if (!objectManager.TryGetHandleWithLogging(info.Ship, out var shipHandle)) return false;

        // A removed piece is null, sent as handle 0
        uint pieceHandle = 0;
        if (info.Piece != null && !objectManager.TryGetHandleWithLogging(info.Piece, out pieceHandle)) return false;

        data = new(shipHandle, info.ShipSlotTag, pieceHandle);
        return true;
    }

    private bool TryPackChangeInfo(ShipFigureheadInfo info, out ShipFigureheadData data)
    {
        data = new();
        if (!objectManager.TryGetHandleWithLogging(info.Ship, out var shipHandle)) return false;

        // A removed figurehead is null, sent as handle 0
        uint figureheadHandle = 0;
        if (info.Figurehead != null && !objectManager.TryGetHandleWithLogging(info.Figurehead, out figureheadHandle)) return false;

        data = new(shipHandle, figureheadHandle);
        return true;
    }

    private bool TryUnpackChangeInfo(ShipRenameData data, out ShipRenameInfo info)
    {
        info = new();
        if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return false;

        info = new(ship, data.NewName);
        return true;
    }

    private bool TryUnpackChangeInfo(ShipUpgradePieceData data, out ShipUpgradePieceInfo info)
    {
        info = new();
        if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return false;

        ShipUpgradePiece piece = null;
        if (data.PieceHandle != 0 && !objectManager.TryGetObjectWithLogging(data.PieceHandle, out piece)) return false;

        info = new(ship, data.ShipSlotTag, piece);
        return true;
    }

    private bool TryUnpackChangeInfo(ShipFigureheadData data, out ShipFigureheadInfo info)
    {
        info = new();
        if (!objectManager.TryGetObjectWithLogging<Ship>(data.ShipHandle, out var ship)) return false;

        Figurehead figurehead = null;
        if (data.FigureheadHandle != 0 && !objectManager.TryGetObjectWithLogging<Figurehead>(data.FigureheadHandle, out figurehead)) return false;

        info = new(ship, figurehead);
        return true;
    }
}
