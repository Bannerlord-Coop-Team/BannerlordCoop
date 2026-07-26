using GameInterface.Services.Inventory.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.Workshops;

/// <summary>
/// Warehouse ItemRosters saved in WorkshopsCampaignBehavior only account for one player
/// This data structure saves a dictionary containing the <Settlement, ItemRoster> KeyValuePairs
/// mapped to individual players using their hero ids.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class WorkshopPlayerData
{
    // Dictionary<PlayerHeroId, KeyValuePair<SettlementId, List<ItemRosterElement>>[]>
    [ProtoMember(1)]
    public Dictionary<string, KeyValuePair<string, List<ItemRosterElementData>>[]> PlayerWarehouseRosterPerSettlement { get; }

    // Dictionary<WorkshopId, WorkshopDataSnapshot>
    [ProtoMember(2)]
    public Dictionary<string, WorkshopDataSnapshot> WorkshopDataByWorkshopId { get; }

    public WorkshopPlayerData(
        Dictionary<string, KeyValuePair<string, List<ItemRosterElementData>>[]> playerWarehouseRosterPerSettlement,
        Dictionary<string, WorkshopDataSnapshot> workshopDataByWorkshopId)
    {
        PlayerWarehouseRosterPerSettlement = playerWarehouseRosterPerSettlement ?? new();
        WorkshopDataByWorkshopId = workshopDataByWorkshopId ?? new();
    }
}

/// <summary>
/// Saved player-workshop production state that vanilla stores in its single-player behavior array.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class WorkshopDataSnapshot
{
    [ProtoMember(1)]
    public bool IsGettingInputsFromWarehouse { get; }

    [ProtoMember(2)]
    public float ProductionProgressForWarehouse { get; }

    [ProtoMember(3)]
    public float ProductionProgressForTown { get; }

    [ProtoMember(4)]
    public float StockProductionInWarehouseRatio { get; }

    public WorkshopDataSnapshot(
        bool isGettingInputsFromWarehouse,
        float productionProgressForWarehouse,
        float productionProgressForTown,
        float stockProductionInWarehouseRatio)
    {
        IsGettingInputsFromWarehouse = isGettingInputsFromWarehouse;
        ProductionProgressForWarehouse = productionProgressForWarehouse;
        ProductionProgressForTown = productionProgressForTown;
        StockProductionInWarehouseRatio = stockProductionInWarehouseRatio;
    }
}
