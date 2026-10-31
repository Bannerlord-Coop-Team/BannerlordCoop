using Common.Messaging;
using Coop.Naval.PortScreen.Data;
using ProtoBuf;
using TaleWorlds.Library;

namespace Coop.Naval.PortScreen.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPortScreenChangesRequested : ICommand
{
    [ProtoMember(1)]
    public readonly PortScreenMode Mode;

    [ProtoMember(2)]
    public readonly uint RightOwnerHandle;

    [ProtoMember(3)]
    public readonly uint LeftOwnerHandle;

    [ProtoMember(4)]
    public readonly uint MainHeroHandle;

    [ProtoMember(5)]
    public readonly uint PlayerClanHandle;

    [ProtoMember(6)]
    public readonly uint MainPartyHandle;

    [ProtoMember(7)]
    public readonly MBReadOnlyList<uint> PackedShipsToBuy;

    [ProtoMember(8)]
    public readonly MBReadOnlyList<uint> PackedShipsToSell;

    [ProtoMember(9)]
    public readonly MBReadOnlyList<uint> PackedShipsToRepair;

    [ProtoMember(10)]
    public readonly MBReadOnlyList<ShipRenameData> PackedShipsToRename;

    [ProtoMember(11)]
    public readonly MBReadOnlyList<ShipUpgradePieceData> PackedSelectedShipPieces;

    [ProtoMember(12)]
    public readonly MBReadOnlyList<ShipFigureheadData> PackedSelectedFigureheads;

    [ProtoMember(13)]
    public readonly MBReadOnlyList<uint> PackedShipsToSend;

    public NetworkPortScreenChangesRequested(
        PortScreenMode mode,
        uint rightOwnerHandle,
        uint leftOwnerHandle,
        uint mainHeroHandle,
        uint playerClanHandle,
        uint mainPartyHandle,
        MBReadOnlyList<uint> packedShipsToBuy,
        MBReadOnlyList<uint> packedShipsToSell,
        MBReadOnlyList<uint> packedShipsToRepair,
        MBReadOnlyList<ShipRenameData> packedShipsToRename,
        MBReadOnlyList<ShipUpgradePieceData> packedSelectedShipPieces,
        MBReadOnlyList<ShipFigureheadData> packedSelectedFigureheads,
        MBReadOnlyList<uint> packedShipsToSend)
    {
        Mode = mode;
        RightOwnerHandle = rightOwnerHandle;
        LeftOwnerHandle = leftOwnerHandle;
        MainHeroHandle = mainHeroHandle;
        PlayerClanHandle = playerClanHandle;
        MainPartyHandle = mainPartyHandle;
        PackedShipsToBuy = packedShipsToBuy;
        PackedShipsToSell = packedShipsToSell;
        PackedShipsToRepair = packedShipsToRepair;
        PackedShipsToRename = packedShipsToRename;
        PackedSelectedShipPieces = packedSelectedShipPieces;
        PackedSelectedFigureheads = packedSelectedFigureheads;
        PackedShipsToSend = packedShipsToSend;
    }
}
