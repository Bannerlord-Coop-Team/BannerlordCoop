using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.WeaponDesigns.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.WeaponDesigns.Patches
{
    /// <summary>
    /// Lifetime Patches for WeaponDesign
    /// </summary>
    [HarmonyPatch]
    internal class WeaponDesignLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<WeaponDesignLifetimePatches>();

        [HarmonyPatch(typeof(WeaponDesign), MethodType.Constructor, new[] { typeof(CraftingTemplate), typeof(TextObject), typeof(WeaponDesignElement[]) })]
        [HarmonyPrefix]
        private static bool CreateWeaponDesignPrefix(ref WeaponDesign __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(WeaponDesign), Environment.StackTrace);
                return true;
            }

            var message = new WeaponDesignCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
