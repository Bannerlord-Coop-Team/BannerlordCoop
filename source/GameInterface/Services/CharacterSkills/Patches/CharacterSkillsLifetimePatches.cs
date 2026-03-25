using Common;
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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(MBCharacterSkills), Environment.StackTrace);
                return false;
            }

            var message = new CharacterSkillsCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
