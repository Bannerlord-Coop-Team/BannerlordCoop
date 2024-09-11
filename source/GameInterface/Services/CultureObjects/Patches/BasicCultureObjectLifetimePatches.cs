using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BasicCultureObjects.Messages;
using GameInterface.Services.CraftingService.Messages;
using GameInterface.Services.CraftingService.Patches;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.BasicCultureObjects.Patches
{
    [HarmonyPatch]
    internal class BasicCultureObjectLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<BasicCultureObjectLifetimePatches>();

        [HarmonyPatch(typeof(BasicCultureObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool ctorPrefix(ref BasicCultureObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(BasicCultureObject), Environment.StackTrace);

                return true;
            }

            if (ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
            {
                var message = new BasicCultureObjectCreated(__instance);

                MessageBroker.Instance.Publish(null, message);
            }

            return true;
        }
    }
}
