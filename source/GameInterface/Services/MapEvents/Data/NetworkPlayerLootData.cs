using GameInterface.Services.TroopRosters.Data;
using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerLootData
{
    [ProtoMember(1)]
    public readonly Dictionary<string, ItemRosterElement[]> LootedItems;

    [ProtoMember(2)]
    public readonly Dictionary<string, TroopRosterData> LootedMembers;

    [ProtoMember(3)]
    public readonly Dictionary<string, TroopRosterData> LootedPrisoners;

    [ProtoMember(4)]
    public readonly Dictionary<string, LootedShipsData> LootedShips;

    public NetworkPlayerLootData(
        Dictionary<string, ItemRosterElement[]> lootedItems,
        Dictionary<string, TroopRosterData> lootedMembers,
        Dictionary<string, TroopRosterData> lootedPrisoners,
        Dictionary<string, LootedShipsData> lootedShips = null)
    {
        LootedItems = lootedItems;
        LootedMembers = lootedMembers;
        LootedPrisoners = lootedPrisoners;
        LootedShips = lootedShips;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct LootedShipsData
{
    [ProtoMember(1)]
    public readonly string[] ShipIds;

    public LootedShipsData(string[] shipIds)
    {
        ShipIds = shipIds;
    }
}
