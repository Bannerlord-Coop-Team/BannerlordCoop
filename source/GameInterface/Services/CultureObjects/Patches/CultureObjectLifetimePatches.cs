using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CultureObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CultureObjects.Patches
{
    [HarmonyPatch]
    internal class CultureObjectLifetimePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CultureObjectLifetimePatches>();

        [HarmonyPatch(typeof(CultureObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool ctorPrefix(ref CultureObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CultureObject), Environment.StackTrace);

                return false;
            }

            var message = new CultureObjectCreated(__instance);

            MessageBroker.Instance.Publish(null, message);

            return true;
        }
    }
}
