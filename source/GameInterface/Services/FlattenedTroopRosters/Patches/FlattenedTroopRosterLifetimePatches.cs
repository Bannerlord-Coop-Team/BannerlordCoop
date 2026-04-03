using System;
using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.FlattenedTroopRosters.Messages;
using GameInterface.Services.TroopRosters.Patches;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.FlattenedTroopRosters.Patches
{
    [HarmonyPatch]
    internal class FlattenedTroopRosterLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<FlattenedTroopRosterLifetimePatches>();

        [HarmonyPatch(typeof(FlattenedTroopRoster), MethodType.Constructor, typeof(int))]
        [HarmonyPrefix]
        private static bool CreateFlattenedTroopRosterPrefix(ref FlattenedTroopRoster __instance, int count)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created managed {name}", typeof(TroopRoster));
                // The original `return false` skipped the constructor body, leaving _elementDictionary null.
                // This caused a NullReferenceException in FlattenedTroopRoster.Add when the conversation
                // UI (MapConversationTableau.FirstTimeInit) tried to render after an encounter started.
                return true;
            }

            var message = new FlattenedTroopRosterCreated(__instance, count);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
