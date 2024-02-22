using HarmonyLib;
using Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace GameInterface.Tests.Bootstrap.Patches;

[HarmonyPatch]
internal class MobilePartyPatches
{
    static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new MethodBase[]
        {
            AccessTools.Method(typeof(MobileParty), nameof(MobileParty.InitializeMobilePartyAroundPosition), new Type[] { typeof(PartyTemplateObject), typeof(Vec2), typeof(float), typeof(float), typeof(int) }),
            AccessTools.Method(typeof(MobilePartyHelper), nameof(MobilePartyHelper.FindReachablePointAroundPosition)),
        };

        return methods;
    }


    private static bool Prefix() => false;
}
