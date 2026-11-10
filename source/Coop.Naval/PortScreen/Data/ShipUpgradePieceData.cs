using ProtoBuf;

namespace Coop.Naval.PortScreen.Data;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ShipUpgradePieceData
{
    [ProtoMember(1)]
    public readonly uint ShipHandle;

    [ProtoMember(2)]
    public readonly string ShipSlotTag;

    [ProtoMember(3)]
    public readonly uint PieceHandle;

    public ShipUpgradePieceData(
        uint shipHandle,
        string shipSlotTag,
        uint pieceHandle)
    {
        ShipHandle = shipHandle;
        ShipSlotTag = shipSlotTag;
        PieceHandle = pieceHandle;
    }
}
