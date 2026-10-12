using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Ships.Messages;

/// <summary>
/// Client request for the server to equip a piece on a ship, where piece handle 0 clears the slot.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkRequestEquipShipUpgradePiece : ICommand
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly string SlotTag;

    [ProtoMember(3)]
    public readonly uint PieceHandle;

    public NetworkRequestEquipShipUpgradePiece(uint shipHandle, string slotTag, uint pieceHandle)
    {
        ShipHandle = shipHandle;
        SlotTag = slotTag;
        PieceHandle = pieceHandle;
    }
}
