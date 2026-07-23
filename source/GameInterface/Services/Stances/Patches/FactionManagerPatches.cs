using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Gates <see cref="FactionManager"/> stance mutations for replication. The war/peace
    /// announcements happen at the action funnels (<see cref="DeclareWarActionPatch"/> /
    /// <see cref="MakePeaceActionPatch"/>); these prefixes only let the stance mutation run on the
    /// server (live) and on the receive path (under AllowedThread), while blocking a client from
    /// originating one. DeclareWar internally calls SetStance, so SetStance must pass through too.
    /// </summary>
    [HarmonyPatch(typeof(FactionManager))]
    internal class FactionManagerPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<FactionManagerPatches>();

        // Faction-elimination stance cleanup (RemoveFactionsFromCampaignWars) is not yet synced.
        [HarmonyPatch("RemoveStance")]
        [HarmonyPrefix]
        private static bool RemoveStancePrefix()
        {
            return false;
        }

        [HarmonyPatch("SetStance")]
        [HarmonyPrefix]
        private static bool SetStancePrefix(MethodBase __originalMethod)
        {
            return AllowStanceMutation(__originalMethod);
        }

        [HarmonyPatch("DeclareWar")]
        [HarmonyPrefix]
        private static bool DeclareWarPrefix(MethodBase __originalMethod)
        {
            return AllowStanceMutation(__originalMethod);
        }

        [HarmonyPatch("SetNeutral")]
        [HarmonyPrefix]
        private static bool SetNeutralPrefix(MethodBase __originalMethod)
        {
            return AllowStanceMutation(__originalMethod);
        }

        private static bool AllowStanceMutation(MethodBase originalMethod)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client called unmanaged FactionManager.{method}", originalMethod.Name);
                return false;
            }

            return true;
        }
    }
}
