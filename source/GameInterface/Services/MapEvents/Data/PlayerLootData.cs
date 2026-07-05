using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Data;

public readonly struct PlayerLootData
{
    public readonly Dictionary<MapEventParty, ItemRoster> LootedItems;
    //public readonly Dictionary<MapEventParty, TroopRosterData>

    public PlayerLootData(Dictionary<MapEventParty, ItemRoster> lootedItems)
    {
        LootedItems = lootedItems;
    }
}
