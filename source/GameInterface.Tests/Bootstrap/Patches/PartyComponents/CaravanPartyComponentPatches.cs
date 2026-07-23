using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch]
internal class CustomPartyComponentPatches
{

    public static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new MethodBase[]
        {
            AccessTools.Method(typeof(CustomPartyComponent.InitializationArgs), nameof(CustomPartyComponent.InitializationArgs.InitializeCustomPartyPropertiesWithPartyTemplate), 
            new Type[] { typeof(MobileParty) }),

            AccessTools.Method(typeof(CustomPartyComponent.InitializationArgs), nameof(CustomPartyComponent.InitializationArgs.InitializeCustomPartyPropertiesWithTroopRoster),
            new Type[] { typeof(MobileParty) }),
        };

        return methods;
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
