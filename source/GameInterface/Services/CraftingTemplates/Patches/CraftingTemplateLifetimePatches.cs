using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CraftingTemplates.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.CraftingTemplates.Patches
{
    [HarmonyPatch]
    public class CraftingTemplateLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingTemplateLifetimePatches>();

        [HarmonyPatch(typeof(CraftingTemplate), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool ctorPrefix(ref CraftingTemplate __instance)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new CraftingTemplateCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(null, message);

            return true;
        }
    }
}
