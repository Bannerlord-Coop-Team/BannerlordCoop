using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.TroopRosters.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Patches
{
    [HarmonyPatch]
    internal class TroopRosterPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<TroopRosterPatches>();

        [HarmonyPatch(typeof(TroopRoster), nameof(TroopRoster.CreateDummyTroopRoster))]
        [HarmonyPrefix]
        private static bool CreateDummyTroopRosterPrefix(ref TroopRoster __result)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            using(new AllowedThread())
            {
                __result = new TroopRoster();
            }

            return false;
        }
    }
}
