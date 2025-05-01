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
            if (CallPolicy.IsOriginalAllowed()) return true;

            if (CallPolicy.SkipIfClient(Logger, out var returnResult)) return returnResult;

            var message = new FlattenedTroopRosterCreated(__instance, count);

            ContainerProvider.TryResolve<IMessageBroker>(out var messageBroker);
            messageBroker?.Publish(__instance, message);

            return true;
        }
    }
}
