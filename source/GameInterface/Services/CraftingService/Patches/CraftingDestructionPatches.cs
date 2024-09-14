using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Patches
{
    internal class CraftingDestructionPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingDestructionPatches>();

        [HarmonyPatch(typeof(Crafting), nameof(Crafting.CreatePreCraftedWeapon))]
        [HarmonyPostfix]
        private static void CreatePreCraftedWeaponPostfix(ref Crafting __instance, ItemObject itemObject, WeaponDesignElement[] usedPieces, string templateId, TextObject weaponName, ItemModifierGroup itemModifierGroup)
        {
            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
                return;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.TryGetId(__instance, out var craftingId);
                objectManager.Remove(__instance);

                var message = new CraftingRemoved(craftingId);

                MessageBroker.Instance.Publish(null, message);
            }
        }

        [HarmonyPatch(typeof(Crafting), nameof(Crafting.InitializePreCraftedWeaponOnLoad))]
        [HarmonyPostfix]
        private static void InitializePreCraftedWeaponOnLoadPostfix(ref Crafting __instance, ItemObject itemObject, WeaponDesign craftedData, TextObject itemName, BasicCultureObject culture)
        {
            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
                return;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.TryGetId(__instance, out var craftingId);
                objectManager.Remove(__instance);

                var message = new CraftingRemoved(craftingId);

                MessageBroker.Instance.Publish(null, message);
            }
        }

        [HarmonyPatch(typeof(Crafting), nameof(Crafting.CreateRandomCraftedItem))]
        [HarmonyPostfix]
        private static void CreateRandomCraftedItemPostfix(ref Crafting __instance, BasicCultureObject culture)
        {
            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
                return;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.TryGetId(__instance, out var craftingId);
                objectManager.Remove(__instance);

                var message = new CraftingRemoved(craftingId);

                MessageBroker.Instance.Publish(null, message);
            }
        }

        [HarmonyPatch(nameof(CraftingState.CraftingLogic), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool CraftingLogicSetterPrefix(ref CraftingState __instance, ref Crafting value)
        {
            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MapEvent), Environment.StackTrace);
                return false;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.TryGetId(__instance, out var craftingId);
                objectManager.Remove(__instance);

                var message = new CraftingRemoved(craftingId);

                MessageBroker.Instance.Publish(null, message);
            }

            return true;
        }
    }
}
