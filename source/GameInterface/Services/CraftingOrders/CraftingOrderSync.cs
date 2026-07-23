using GameInterface.AutoSync;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CraftingSystem;

namespace GameInterface.Services.Settlements;
internal class CraftingOrderSync : IAutoSync
{
    public CraftingOrderSync(AutoSyncRegistry AutoSyncRegistry)
    {
        //// Fields
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.BaseGoldReward))); // readonly
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.OrderDifficulty))); // readonly
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder._weaponDesignTemplate)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.PreCraftedWeaponDesignItem)));
        //AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder._preCraftedWeaponDesignItemData))); // Manually synced
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.OrderOwner)));
        AutoSyncRegistry.AddField(AccessTools.Field(typeof(CraftingOrder), nameof(CraftingOrder.DifficultyLevel))); // readonly
    }
}
