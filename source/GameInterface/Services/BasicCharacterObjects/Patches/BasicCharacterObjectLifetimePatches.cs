using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.BasicCharacterObjects.Messages;
using GameInterface.Services.CharacterObjects.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.CharacterObjects.Patches
{
    /// <summary>
    /// Lifetime Patches for BasicCharacterObjects
    /// </summary>
    [HarmonyPatch]
    internal class BasicCharacterObjectLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<BasicCharacterObjectLifetimePatches>();

        [HarmonyPatch(typeof(BasicCharacterObject), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateCharacterObjectPrefix(ref BasicCharacterObject __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(BasicCharacterObject), Environment.StackTrace);
                return false;
            }

            var message = new BasicCharacterCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
