using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Ships.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShipPiecesChanged : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly string[] SlotTags;

    [ProtoMember(3)]
    public readonly uint[] PieceHandles;

    [ProtoMember(4)]
    public readonly uint[] UnlockedPieceHandles;

    [ProtoMember(5)]
    public readonly bool HasUnlockedPieces;

    public NetworkShipPiecesChanged(
        uint shipHandle,
        string[] slotTags,
        uint[] pieceHandles,
        uint[] unlockedPieceHandles)
    {
        ShipHandle = shipHandle;
        SlotTags = slotTags;
        PieceHandles = pieceHandles;
        UnlockedPieceHandles = unlockedPieceHandles;
        HasUnlockedPieces = unlockedPieceHandles != null;
    }
}
