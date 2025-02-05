using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Monsters.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.Monsters.Patches
{
    /// <summary>
    /// Lifetime Patches for Monsters
    /// </summary>

    [HarmonyPatch]
    internal class MonsterLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<MonsterLifetimePatches>();

        [HarmonyPatch(typeof(Monster), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateMonsterPrefix(ref Monster __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(Monster), Environment.StackTrace);
                return false;
            }

            var message = new MonsterCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
