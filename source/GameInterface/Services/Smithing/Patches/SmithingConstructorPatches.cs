using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches;

[HarmonyPatch(typeof(CraftingVM))]
internal class CraftingVMConstructorPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<CraftingVM>();

    [HarmonyPatch(MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(Crafting), typeof(Action), typeof(Action), typeof(Action), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>) })]
    [HarmonyPostfix]
    public static void CraftingVMConstructorPostfix(CraftingVM __instance, Crafting crafting, Action onClose, Action resetCamera, Action onWeaponCrafted, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags)
    {
        // Call original if we call this function
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        MessageBroker.Instance.Publish(__instance, new CraftingVMCreated(__instance));
    }
}
