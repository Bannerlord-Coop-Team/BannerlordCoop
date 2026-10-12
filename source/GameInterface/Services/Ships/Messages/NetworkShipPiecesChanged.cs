using Common.Messaging;
using GameInterface.Services.Ships.Data;
using ProtoBuf;

namespace GameInterface.Services.Ships.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkShipPiecesChanged : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly ShipSlotPieceData[] Slots;

    [ProtoMember(3)]
    public readonly uint[] UnlockedPieceHandles;

    // Protobuf reads a null array back as empty, and vanilla treats a null unlocked list differently
    [ProtoMember(4)]
    public readonly bool HasUnlockedPieces;

    public NetworkShipPiecesChanged(
        uint shipHandle,
        ShipSlotPieceData[] slots,
        uint[] unlockedPieceHandles)
    {
        ShipHandle = shipHandle;
        Slots = slots;
        UnlockedPieceHandles = unlockedPieceHandles;
        HasUnlockedPieces = unlockedPieceHandles != null;
    }
}
