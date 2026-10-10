using GameInterface.Services.Inventory.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct ForceTransferPoolData
{
    [ProtoMember(1)]
    public readonly VillageHostileAction Action;
    [ProtoMember(2)]
    public readonly string PartyId;
    [ProtoMember(3)]
    public readonly string SettlementId;
    [ProtoMember(4)]
    public readonly string RequestId;
    [ProtoMember(5)]
    public readonly ItemRosterElementData[] SuppliesItems;
    [ProtoMember(6)]
    public readonly string TroopId;
    [ProtoMember(7)]
    public readonly int TroopCount;

    public ForceTransferPoolData(
        VillageHostileAction action,
        string partyId,
        string settlementId,
        string requestId,
        ItemRosterElementData[] suppliesItems,
        string troopId,
        int troopCount)
    {
        Action = action;
        PartyId = partyId;
        SettlementId = settlementId;
        RequestId = requestId;
        SuppliesItems = suppliesItems;
        TroopId = troopId;
        TroopCount = troopCount;
    }
}
