using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch]
internal class MobilePartyPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new MethodBase[]
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.InitializeMobilePartyAroundPosition), new Type[] { typeof(TroopRoster), typeof(TroopRoster), typeof(CampaignVec2), typeof(float), typeof(float), typeof(bool) }),
            //AccessTools.Method(typeof(MobilePartyHelper), nameof(MobilePartyHelper.FindReachablePointAroundPosition)),
        };

        return methods;
    }


    private static bool Prefix() => false;
}
