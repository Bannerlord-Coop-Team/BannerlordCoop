using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch(typeof(SPInventoryVM))]
internal class SPInventoryVMPatches
{
    [HarmonyPatch(nameof(SPInventoryVM.SaveItemLockStates))]
    [HarmonyPostfix]
    public static void SaveItemLockStatesPostfix(SPInventoryVM __instance)
    {
        // Send full list of inventory locks to server. This is reset locally every time
        var message = new SaveItemLockStates(Hero.MainHero, __instance._viewDataTracker.GetInventoryLocks());
        MessageBroker.Instance.Publish(__instance, message);
    }

    [HarmonyPatch(nameof(SPInventoryVM.SaveItemSortStates))]
    [HarmonyPostfix]
    public static void SaveItemSortStatesPostfix(SPInventoryVM __instance)
    {
        var usageType = (int)__instance._usageType;
        var sortPreference = __instance._viewDataTracker.InventoryGetSortPreference(usageType);

        var message = new SaveItemSortStates(Hero.MainHero, usageType, sortPreference);
        MessageBroker.Instance.Publish(__instance, message);
    }
}
