using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.EquipmentRoster.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.EquipmentRoster.Patches
{
    /// <summary>
    /// Lifetime Patches for EquipmentRoster
    /// </summary>
    [HarmonyPatch]
    internal class EquipmentRosterLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<EquipmentRosterLifetimePatches>();

        [HarmonyPatch(typeof(MBEquipmentRoster), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateEquipmentRosterPrefix(ref MBEquipmentRoster __instance)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new EquipmentRosterCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
