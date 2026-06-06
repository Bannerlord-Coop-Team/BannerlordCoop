using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch]
internal class MilitiaPartyComponentPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new MethodBase[]
        {
            AccessTools.Method(typeof(MilitiaPartyComponent.InitializationArgs), nameof(MilitiaPartyComponent.InitializationArgs.InitializeMilitiaPartyProperties),
            new Type[] { typeof(MobileParty), typeof(Settlement) }),
        };

        return methods;
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
