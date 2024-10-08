using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GameInterface.Services.GameDebug.Patches;

[HarmonyPatch(typeof(MobileParty))]
class TestPatch
{
    static ILogger Logger = LogManager.GetLogger<TestPatch>();
    static object _lock = new object();

    //[HarmonyPatch(nameof(MobileParty.InitializeCachedPartyVariables))]
    //[HarmonyPrefix]
    static bool ParallelInitializeCachedPartyVariablesPrefix(MobileParty __instance, ref CachedPartyVariables variables)
    {
        try
        {
            if (__instance.Party == null) return false;

            variables.HasMapEvent = __instance.MapEvent != null;
            variables.CurrentPosition = __instance.Position2D;
            variables.TargetPartyPositionAtFrameStart = Vec2.Invalid;
            variables.LastCurrentPosition = __instance.Position2D;
            variables.IsAttachedArmyMember = false;
            variables.IsMoving = __instance.IsMoving || __instance.IsMainParty;
            variables.IsArmyLeader = false;
            if (__instance.Army != null)
            {
                if (__instance.Army.LeaderParty == __instance)
                {
                    variables.IsArmyLeader = true;
                }
                else if (__instance.Army.LeaderParty.AttachedParties.Contains(__instance))
                {
                    variables.IsAttachedArmyMember = true;
                    variables.IsMoving = __instance.IsMoving || __instance.Army.LeaderParty.IsMoving;
                }
            }
        }
        catch(Exception e)
        {
            Logger.Error("Parallel stuff got the following error: {error}", e);
        }

        return false;
    }
}
