using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CharacterSkills.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterSkills.Patches
{
    /// <summary>
    /// Lifetime Patches for CharacterSkills
    /// </summary>
    [HarmonyPatch]
    internal class CharacterSkillsLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<CharacterSkillsLifetimePatches>();

        [HarmonyPatch(typeof(MBCharacterSkills), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateCharacterSkillsPrefix(ref MBCharacterSkills __instance)
        {
            // Call original if we call this function
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new CharacterSkillsCreated(__instance);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);

            messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
