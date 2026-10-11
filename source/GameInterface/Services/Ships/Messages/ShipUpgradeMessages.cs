using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;

namespace GameInterface.Services.Ships.Messages;

/// <summary>Local event raised on the server after a ship's upgrade slot changed.</summary>
internal readonly struct ShipUpgradePieceChanged : IEvent
{
    public readonly Ship Ship;
    public readonly string SlotTag;
    public readonly ShipUpgradePiece Piece;

    public ShipUpgradePieceChanged(Ship ship, string slotTag, ShipUpgradePiece piece)
    {
        Ship = ship;
        SlotTag = slotTag;
        Piece = piece;
    }
}

/// <summary>Sets one upgrade slot of a ship; a zero piece handle empties the slot.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetShipUpgradePiece : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;
    [ProtoMember(2)]
    public readonly string SlotTag;
    [ProtoMember(3)]
    public readonly uint PieceHandle;

    public NetworkSetShipUpgradePiece(uint shipHandle, string slotTag, uint pieceHandle)
    {
        ShipHandle = shipHandle;
        SlotTag = slotTag;
        PieceHandle = pieceHandle;
    }
}

/// <summary>Local event raised on the server after a ship's figurehead was changed.</summary>
internal readonly struct ShipFigureheadChanged : IEvent
{
    public readonly Ship Ship;

    public ShipFigureheadChanged(Ship ship)
    {
        Ship = ship;
    }
}

/// <summary>Sets a ship's figurehead; a zero figurehead handle removes it.</summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkChangeShipFigurehead : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;
    [ProtoMember(2)]
    public readonly uint FigureheadHandle;

    public NetworkChangeShipFigurehead(uint shipHandle, uint figureheadHandle)
    {
        ShipHandle = shipHandle;
        FigureheadHandle = figureheadHandle;
    }
}

/// <summary>Local event raised on the server after a ship's unlocked upgrade pieces changed.</summary>
internal readonly struct ShipUnlockedUpgradePiecesChanged : IEvent
{
    public readonly Ship Ship;

    public ShipUnlockedUpgradePiecesChanged(Ship ship)
    {
        Ship = ship;
    }
}

/// <summary>
/// Replaces a ship's unlocked upgrade pieces. <see cref="IsNull"/> mirrors vanilla's null list for ships no player owns.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetShipUnlockedUpgradePieces : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;
    [ProtoMember(2)]
    public readonly bool IsNull;
    [ProtoMember(3)]
    public readonly uint[] PieceHandles;

    public NetworkSetShipUnlockedUpgradePieces(uint shipHandle, bool isNull, uint[] pieceHandles)
    {
        ShipHandle = shipHandle;
        IsNull = isNull;
        PieceHandles = pieceHandles;
    }
}
