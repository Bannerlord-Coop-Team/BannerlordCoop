using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.WeaponDesigns.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Reflection;
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

        [HarmonyPatch(typeof(WeaponDesign))]
        [HarmonyTargetMethod]
        private static MethodBase TargetConstructor()
        {
            var ctors = typeof(WeaponDesign).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var ctor in ctors)
            {
                var p = ctor.GetParameters();
                if (p.Length >= 3 &&
                    p[0].ParameterType.FullName == "TaleWorlds.Core.CraftingTemplate" &&
                    p[1].ParameterType.FullName == "TaleWorlds.Localization.TextObject")
                {
                    var third = p[2].ParameterType;
                    if ((third.IsArray && third.GetElementType()?.FullName == "TaleWorlds.Core.WeaponDesignElement") ||
                        (third.IsGenericType && third.GetGenericArguments()[0].FullName == "TaleWorlds.Core.WeaponDesignElement"))
                    {
                        return ctor;
                    }
                }
            }
            return null;
        }

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

        [HarmonyPrepare]
        private static bool Prepare()
        {
            return TargetConstructor() != null;
        }
    }
}
