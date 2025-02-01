using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BasicCultureObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.BasicCultureObjects.Patches
{
    /// <summary>
    /// Lifetime Patches for BasicCultureObjects
    /// </summary>
    [HarmonyPatch]
    internal class BasicCultureObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<BasicCultureObjectLifetimePatches>();

        [HarmonyPatch(typeof(BasicCultureObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateBasicCultureObjectPrefix(ref BasicCultureObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(BasicCultureObject), Environment.StackTrace);
                return false;
            }

            var message = new BasicCultureCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
