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

    public NetworkShipPiecesChanged(
        uint shipHandle,
        string[] slotTags,
        uint[] pieceHandles)
    {
        ShipHandle = shipHandle;
        SlotTags = slotTags;
        PieceHandles = pieceHandles;
    }
}
