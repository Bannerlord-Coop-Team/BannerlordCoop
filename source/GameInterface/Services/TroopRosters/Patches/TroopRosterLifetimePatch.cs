using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.TroopRosters.Patches
{
    /// <summary>
    /// Lifetime Patches for TroopRoster
    /// </summary>
    [HarmonyPatch]
    internal class TroopRosterLifetimePatches
    {
        private static ILogger Logger = LogManager.GetLogger<TroopRosterLifetimePatches>();

        [HarmonyPatch(typeof(TroopRoster), MethodType.Constructor)]
        [HarmonyPrefix]
        private static bool CreateTroopRosterPrefix(ref TroopRoster __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client created unmanaged {name}\n"
                    + "Callstack: {callstack}", typeof(TroopRoster), Environment.StackTrace);
                return false;
            }

            var message = new TroopRosterCreated(__instance);

            MessageBroker.Instance.Publish(__instance, message);

            return true;
        }
    }
}
