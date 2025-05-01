using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingService.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.GameState;
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
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new CraftingCreated(__instance, craftingTemplate, culture, name);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);

            messageBroker?.Publish(null, message);

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
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new CraftingRemoved(value);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(null, message);

            return true;
        }
    }
}
