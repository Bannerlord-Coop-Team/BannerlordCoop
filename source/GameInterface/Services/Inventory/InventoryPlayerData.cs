using ProtoBuf;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Inventory;

/// <summary>
/// Some data structures for managing the inventory are player specific and have to be managed separately
/// _inventoryItemLocks is unique for each player, need unique list per player
/// _inventorySortPreferences is unique for each player, need unique list per player
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class InventoryPlayerData
{
    // Dictionary<PlayerHeroId, List<ItemId>>
    [ProtoMember(1)]
    public Dictionary<string, List<string>> PlayerInventoryLocks { get; }

    // Dictionary<PlayerHeroId, Dictionary<UsageType, Preference>
    [ProtoMember(2)]
    public Dictionary<string, Dictionary<int, Tuple<int, int>>> PlayerInventorySortPreferences { get; }

    public InventoryPlayerData(
        Dictionary<string, List<string>> playerInventoryLocks,
        Dictionary<string, Dictionary<int, Tuple<int, int>>> playerInventorySortPreferences)
    {
        PlayerInventoryLocks = playerInventoryLocks;
        PlayerInventorySortPreferences = playerInventorySortPreferences;
    }
}
