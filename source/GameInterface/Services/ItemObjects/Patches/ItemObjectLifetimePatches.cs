using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.ItemObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects.Patches
{
    /// <summary>
    /// Lifetime Patches for ItemObjects
    /// </summary>

    //Registry crashes it, no need for lifetime without it

    [HarmonyPatch]
    internal class ItemObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<ItemObjectLifetimePatches>();

        [HarmonyPatch(typeof(ItemObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateBuildingPrefix(ref ItemObject __instance)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new ItemObjectCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
