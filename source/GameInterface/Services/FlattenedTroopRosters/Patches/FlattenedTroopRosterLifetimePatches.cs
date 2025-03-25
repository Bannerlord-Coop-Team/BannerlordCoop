using System;
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
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(TroopRoster), Environment.StackTrace);
                return false;
            }

            var message = new FlattenedTroopRosterCreated(__instance, count);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
