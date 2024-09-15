using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch]
    public class CraftingLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingLifetimePatches>();

        [HarmonyPatch(typeof(Crafting), MethodType.Constructor, typeof(CraftingTemplate), typeof(BasicCultureObject), typeof(TextObject))]
        [HarmonyPrefix]
        private static bool CreateCraftingPrefix(ref Crafting __instance, CraftingTemplate craftingTemplate, BasicCultureObject culture, TextObject name)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Crafting), Environment.StackTrace);

                return true;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                objectManager.AddNewObject(__instance, out var newId);

                var data = new CraftingCreatedData(newId, craftingTemplate.StringId, culture.StringId, name.Value);
                var message = new CraftingCreated(data);

                MessageBroker.Instance.Publish(null, message);
            }

            return true;
        }

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

            var message = new CraftingRemoved(__instance);

            MessageBroker.Instance.Publish(null, message);
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

            var message = new CraftingRemoved(__instance);

            MessageBroker.Instance.Publish(null, message);
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

            var message = new CraftingRemoved(__instance);

            MessageBroker.Instance.Publish(null, message);
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

            var message = new CraftingRemoved(value);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }
}
