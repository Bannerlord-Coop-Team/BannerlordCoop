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
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new MonsterCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
