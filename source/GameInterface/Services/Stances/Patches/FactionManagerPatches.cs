using Common;
using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using System;
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

        [HarmonyPatch("IsAtWarAgainstFaction")]
        [HarmonyPostfix]
        private static void IsAtWarAgainstFactionPostfix(IFaction faction1, IFaction faction2, ref bool __result)
        {
            if (__result)
                return;

            if (faction1 == null || faction2 == null || faction1 == faction2)
                return;

            if (faction1.IsEliminated || faction2.IsEliminated)
                return;

            if (HasFactionWar(faction1, faction2) && HasFactionWar(faction2, faction1))
                __result = true;
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

        private static bool HasFactionWar(IFaction faction, IFaction otherFaction)
        {
            try
            {
                return faction.FactionsAtWarWith?.Contains(otherFaction) == true;
            }
            catch (NullReferenceException)
            {
                return false;
            }
        }
    }
}
