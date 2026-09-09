using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Interfaces;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Smithing.Patches;

[HarmonyPatch(typeof(CraftingCampaignBehavior))]
internal class CreateCraftedWeaponInternalPatch
{
    private const string ClientVisualPrefix = "ClientVisual_";
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInternal))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInternalPrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, bool isFreeMode, Hero crafterHero, WeaponDesign weaponDesign, ItemModifier weaponModifier = null)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        // The unique client request id also gives the temporary item the string id needed to render the result.
        string clientRequestId = Guid.NewGuid().ToString("N");
        string nextCraftedItemId = $"{ClientVisualPrefix}{clientRequestId}";
        ItemObject craftedItemObject;
        using (new AllowedThread())
        {
            craftedItemObject = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic.GetCurrentCraftedItemObject(true, nextCraftedItemId);
            ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);

            ItemObject registeredObject = MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);
        }
        Crafting craftingLogic = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic;

        ContainerProvider.TryResolve<ISmithingVMsProvider>(out var smithingVMsProvider);
        var activeCraftingOrder = smithingVMsProvider.GetActiveCraftingOrder();

        // Need to return the ItemObject for client's CraftingVM
        __result = craftedItemObject;

        // Publish message with data. Local ClientVisual not sent.
        // Actual item created on server and all clients in CraftingCampaignBehaviorCraftingHandler.
        var message = new CreatedCraftedWeaponInternal(
            isFreeMode,
            crafterHero,
            craftedItemObject.Name,
            craftedItemObject.Culture,
            weaponDesign,
            weaponModifier,
            Hero.MainHero,
            craftingLogic,
            activeCraftingOrder,
            Settlement.CurrentSettlement,
            clientRequestId);
        MessageBroker.Instance.Publish(__instance, message);

        // Skip original to override original client saving
        return false;
    }

    [HarmonyPatch(typeof(WeaponDesignVM), nameof(WeaponDesignVM.CreateCraftingResultPopup))]
    [HarmonyPrefix]
    public static bool CreateCraftingResultPopupPrefix(ref WeaponDesignVM __instance)
    {
        if (!IsPendingCraftedItem(__instance.CraftedItemObject)) return true;

        __instance.IsInFinalCraftingStage = false;
        return false;
    }

    [HarmonyPatch(typeof(CraftingVM), nameof(CraftingVM.ExecuteMainAction))]
    [HarmonyPrefix]
    public static bool ExecuteMainActionPrefix(CraftingVM __instance)
    {
        return !IsPendingCraftedItem(__instance.WeaponDesign?.CraftedItemObject);
    }

    [HarmonyPatch(typeof(WeaponDesignVM), nameof(WeaponDesignVM.OnFinalize))]
    [HarmonyPrefix]
    public static void WeaponDesignVMOnFinalizePrefix(WeaponDesignVM __instance)
    {
        ClearPendingCraftedItem(__instance);
    }

    private static bool IsPendingCraftedItem(ItemObject craftedItem)
        => craftedItem?.StringId?.StartsWith(ClientVisualPrefix, StringComparison.Ordinal) == true;

    private static bool IsPendingCraftedItem(ItemObject craftedItem, string clientRequestId)
        => IsPendingCraftedItem(craftedItem) &&
           string.Equals(craftedItem.StringId, $"{ClientVisualPrefix}{clientRequestId}", StringComparison.Ordinal);

    public static bool ClearPendingCraftedItem(WeaponDesignVM weaponDesignVM)
    {
        if (weaponDesignVM == null || !IsPendingCraftedItem(weaponDesignVM.CraftedItemObject)) return false;

        string clientRequestId = weaponDesignVM.CraftedItemObject.StringId.Substring(ClientVisualPrefix.Length);
        return ClearPendingCraftedItem(weaponDesignVM, clientRequestId);
    }

    public static bool ClearPendingCraftedItem(WeaponDesignVM weaponDesignVM, string clientRequestId)
    {
        if (weaponDesignVM == null || !IsPendingCraftedItem(weaponDesignVM.CraftedItemObject, clientRequestId)) return false;

        var pendingCraftedItem = weaponDesignVM.CraftedItemObject;
        MBObjectManager.Instance.UnregisterObject(pendingCraftedItem);

        if (GameStateManager.Current.ActiveState is CraftingState craftingState &&
            ReferenceEquals(craftingState.CraftingLogic._craftedItemObject, pendingCraftedItem))
        {
            craftingState.CraftingLogic._craftedItemObject = null;
        }

        weaponDesignVM.CraftedItemObject = null;
        weaponDesignVM.IsInFinalCraftingStage = false;
        return true;
    }

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInCraftingOrderMode))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInCraftingOrderModePrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, Hero crafterHero, CraftingOrder craftingOrder, WeaponDesign weaponDesign)
    {
        ItemObject itemObject = __instance.CreateCraftedWeaponInternal(false, crafterHero, weaponDesign, __instance._currentItemModifier);

        __result = itemObject;
        return false;
    }

    [HarmonyPatch(nameof(CraftingCampaignBehavior.CreateCraftedWeaponInFreeBuildMode))]
    [HarmonyPrefix]
    public static bool CreateCraftedWeaponInFreeBuildModePrefix(CraftingCampaignBehavior __instance, ref ItemObject __result, Hero hero, WeaponDesign weaponDesign, ItemModifier weaponModifier = null)
    {
        ItemObject itemObject = __instance.CreateCraftedWeaponInternal(true, hero, weaponDesign, weaponModifier);

        __result = itemObject;
        return false;
    }
}
