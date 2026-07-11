using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.MapEvents.Data;

public readonly struct PlayerLootData
{
    public readonly Dictionary<MapEventParty, ItemRoster> LootedItems;
    public readonly Dictionary<MapEventParty, TroopRoster> LootedMembers;
    public readonly Dictionary<MapEventParty, TroopRoster> LootedPrisoners;

    public PlayerLootData(
        Dictionary<MapEventParty, ItemRoster> lootedItems,
        Dictionary<MapEventParty, TroopRoster> lootedMembers,
        Dictionary<MapEventParty, TroopRoster> lootedPrisoners)
    {
        LootedItems = lootedItems;
        LootedMembers = lootedMembers;
        LootedPrisoners = lootedPrisoners;
    }
}
