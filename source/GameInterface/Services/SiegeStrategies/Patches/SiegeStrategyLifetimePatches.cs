using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.SiegeStrategies.Patches;
using GameInterface.Services.SiegeStrategies.Messages.Lifetime;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Patches
{
    /// <summary>
    /// Lifetime Patches for SiegeStrategies
    /// </summary>
    [HarmonyPatch]
    internal class SiegeStrategyLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<SiegeStrategyLifetimePatches>();

        [HarmonyPatch(typeof(SiegeStrategy), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateSiegeStrategyPrefix(ref SiegeStrategy __instance)
        { 
        // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created managed {name}", typeof(SiegeStrategy));
                return true;
            }

            var message = new SiegeStrategyCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
