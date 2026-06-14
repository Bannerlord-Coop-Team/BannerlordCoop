using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Stances.Patches
{
    /// <summary>
    /// Routes <see cref="FactionManager"/> stance mutations through coop sync. The war/peace
    /// announcements happen at the action funnels (<see cref="DeclareWarActionPatch"/> /
    /// <see cref="MakePeaceActionPatch"/>); these prefixes only let the stance mutation run on the
    /// server (live) and on the receive path (under AllowedThread), while blocking a client from
    /// originating one. DeclareWar internally calls SetStance, so SetStance must pass through too.
    /// </summary>
    [HarmonyPatch(typeof(FactionManager))]
    internal class FactionManagerPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<FactionManagerPatches>();

        [HarmonyPatch("AddStance")]
        [HarmonyPrefix]
        private static bool AddStancePrefix()
        {
            return true;
        }

        // Faction-elimination stance cleanup (RemoveFactionsFromCampaignWars) is not yet synced.
        [HarmonyPatch("RemoveStance")]
        [HarmonyPrefix]
        private static bool RemoveStancePrefix()
        {
            return false;
        }

        [HarmonyPatch("SetStance")]
        [HarmonyPrefix]
        private static bool SetStancePrefix()
        {
            // SetStance is private, so it cannot be referenced via nameof from here.
            return AllowStanceMutation("SetStance");
        }

        [HarmonyPatch("DeclareWar")]
        [HarmonyPrefix]
        private static bool DeclareWarPrefix()
        {
            return AllowStanceMutation(nameof(FactionManager.DeclareWar));
        }

        [HarmonyPatch("SetNeutral")]
        [HarmonyPrefix]
        private static bool SetNeutralPrefix()
        {
            return AllowStanceMutation(nameof(FactionManager.SetNeutral));
        }

        private static bool AllowStanceMutation(string method)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (ModInformation.IsClient)
            {
                Logger.Error("Client called unmanaged FactionManager.{method}", method);
                return false;
            }

            return true;
        }
    }
}
