using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingService.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch(typeof(Crafting))]
    public class CraftingLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingLifetimePatches>();

        [HarmonyPatch(typeof(Crafting), MethodType.Constructor, typeof(CraftingTemplate), typeof(BasicCultureObject), typeof(TextObject))]
        [HarmonyPrefix]
        static bool CreateCraftingPrefix(ref Crafting __instance, CraftingTemplate craftingTemplate, BasicCultureObject culture, TextObject name)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Crafting), Environment.StackTrace);

                return true;
            }

            var message = new CraftingCreated(__instance, craftingTemplate, culture, name);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }

    [HarmonyPatch(typeof(CraftingState))]
    public class CraftingStateLifetimePatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingStateLifetimePatch>();

        [HarmonyPatch(nameof(CraftingState.CraftingLogic), MethodType.Setter)]
        [HarmonyPrefix]
        static bool Prefix(ref CraftingState __instance, ref Crafting value)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CraftingState), Environment.StackTrace);
                return false;
            }

            var message = new CraftingRemoved(value);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }
}
