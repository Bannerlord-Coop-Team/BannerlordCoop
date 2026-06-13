using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Workshops;

/// <summary>
/// Warehouse ItemRosters saved in WorkshopsCampaignBehavior only account for one player
/// This data structure saves a dictionary containing the <Settlement, ItemRoster> KeyValuePairs
/// mapped to individual players using their hero ids.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class WorkshopPlayerData
{
    // Dictionary<PlayerHeroId, KeyValuePair<SettlementId, ItemRosterId>[]>
    [ProtoMember(1)]
    public Dictionary<string, KeyValuePair<string, string>[]> PlayerWarehouseRosterPerSettlement { get; }

    public WorkshopPlayerData(Dictionary<string, KeyValuePair<string, string>[]> playerWarehouseRosterPerSettlement)
    {
        PlayerWarehouseRosterPerSettlement = playerWarehouseRosterPerSettlement;
    }
}
