using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.Patches;

[HarmonyPatch(typeof(SPInventoryVM))]
internal class SPInventoryVMConstructorPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<SPInventoryVM>();

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(InventoryLogic), typeof(bool), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>) })]
    [HarmonyPostfix]
    public static void SPInventoryVMConstructorPostfix(SPInventoryVM __instance, InventoryLogic inventoryLogic, bool isInCivilianModeByDefault, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags)
    {
        MessageBroker.Instance.Publish(__instance, new InventoryVMCreated(__instance));
    }
}
