using GameInterface.DynamicSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CraftingSystem;

namespace GameInterface.Services.Settlements;
internal class CraftingOrderSync : IDynamicSync
{
    public CraftingOrderSync(DynamicSyncRegistry dynamicSyncRegistry)
    {
        //// Fields
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.BaseGoldReward))); // readonly
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.OrderDifficulty))); // readonly
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder._weaponDesignTemplate)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.PreCraftedWeaponDesignItem)));
        //dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder._preCraftedWeaponDesignItemData))); // Manually synced
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.OrderOwner)));
        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.DifficultyLevel))); // readonly
    }
}
