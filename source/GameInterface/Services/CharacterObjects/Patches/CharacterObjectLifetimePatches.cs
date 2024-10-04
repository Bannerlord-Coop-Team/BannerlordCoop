using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.CharacterObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.CharacterObjects.Patches
{
    /// <summary>
    /// Lifetime Patches for CharacterObjects
    /// </summary>
    [HarmonyPatch]
    internal class CharacterObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<CharacterObjectLifetimePatches>();

        [HarmonyPatch(typeof(CharacterObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateCharacterObjectPrefix(ref CharacterObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(CharacterObject), Environment.StackTrace);
                return true;
            }

            var message = new CharacterObjectCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
