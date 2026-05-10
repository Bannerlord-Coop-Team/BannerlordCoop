using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Refinement;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(SmeltingVM))]
    internal class SmeltingVMConstructorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SmeltingVM>();

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Action), typeof(Action) })]
        [HarmonyPostfix]
        public static void SmeltingVMConstructorPostfix(SmeltingVM __instance, Action updateValuesOnSelectItemAction, Action updateValuesOnSmeltItemAction)
        {
            Logger.Information("SmeltingVM constructor patched");
            MessageBroker.Instance.Publish(__instance, new SmeltingVMCreated(__instance));
        }
    }

    [HarmonyPatch(typeof(RefinementVM))]
    internal class RefinementVMConstructorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<RefinementVM>();

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Action), typeof(Func<CraftingAvailableHeroItemVM>) })]
        [HarmonyPostfix]
        public static void RefinementVMConstructorPostfix(RefinementVM __instance, Action onRefinementSelectionChange, Func<CraftingAvailableHeroItemVM> getCurrentHero)
        {
            Logger.Information("RefinementVM constructor patched");
            MessageBroker.Instance.Publish(__instance, new RefinementVMCreated(__instance));
        }
    }

    [HarmonyPatch(typeof(CraftingVM))]
    internal class CraftingVMConstructorPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingVM>();

        [HarmonyPatch(MethodType.Constructor)]
        [HarmonyPatch(new Type[] { typeof(Crafting), typeof(Action), typeof(Action), typeof(Action), typeof(Func<WeaponComponentData, ItemObject.ItemUsageSetFlags>) })]
        [HarmonyPostfix]
        public static void CraftingVMConstructorPostfix(CraftingVM __instance, Crafting crafting, Action onClose, Action resetCamera, Action onWeaponCrafted, Func<WeaponComponentData, ItemObject.ItemUsageSetFlags> getItemUsageSetFlags)
        {
            Logger.Information("CraftingVM constructor patched");
            MessageBroker.Instance.Publish(__instance, new CraftingVMCreated(__instance));
        }
    }
}