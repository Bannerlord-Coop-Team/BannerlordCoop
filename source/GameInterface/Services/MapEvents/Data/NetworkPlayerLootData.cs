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

    public NetworkPlayerLootData(
        Dictionary<string, ItemRosterElement[]> lootedItems,
        Dictionary<string, TroopRosterData> lootedMembers,
        Dictionary<string, TroopRosterData> lootedPrisoners)
    {
        LootedItems = lootedItems;
        LootedMembers = lootedMembers;
        LootedPrisoners = lootedPrisoners;
    }
}
