using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ItemObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace GameInterface.Services.Buildings.Patches
{
    /// <summary>
    /// Lifetime Patches for Buildings
    /// </summary>
    [HarmonyPatch]
    internal class ItemObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<ItemObjectLifetimePatches>();

        [HarmonyPatch(typeof(ItemObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateBuildingPrefix(ref ItemObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(ItemObject), Environment.StackTrace);
                return true;
            }

            var message = new ItemObjectCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
