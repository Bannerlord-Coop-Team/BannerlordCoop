using ProtoBuf;

namespace GameInterface.Services.Ships.Data;

/// <summary>
/// Wire form of one ship slot and the piece equipped in it, where handle 0 is an empty slot.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct ShipSlotPieceData
{
    [ProtoMember(1)]
    public readonly string SlotTag;

    [ProtoMember(2)]
    public readonly uint PieceHandle;

    public ShipSlotPieceData(string slotTag, uint pieceHandle)
    {
        SlotTag = slotTag;
        PieceHandle = pieceHandle;
    }
}
