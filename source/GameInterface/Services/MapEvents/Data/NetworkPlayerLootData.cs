using ProtoBuf;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPlayerLootData
{
    [ProtoMember(1)]
    public readonly Dictionary<string, ItemRosterElement[]> LootedItems;

    //[ProtoMember(2)]
    //public readonly Dictionary<string, TroopRosterElementData[]> LootedTroops; // ??

    public NetworkPlayerLootData(Dictionary<string, ItemRosterElement[]> lootedItems)
    {
        LootedItems = lootedItems;
    }
}
