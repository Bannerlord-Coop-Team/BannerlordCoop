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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CraftingTemplate), Environment.StackTrace);

                return true;
            }

            var message = new CraftingTemplateCreated(__instance);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }
}
