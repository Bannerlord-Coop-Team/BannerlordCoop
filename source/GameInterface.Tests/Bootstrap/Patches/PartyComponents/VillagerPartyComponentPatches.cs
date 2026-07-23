using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch]
internal class VillagerPartyComponentPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new MethodBase[]
        {
            AccessTools.Method(typeof(VillagerPartyComponent.InitializationArgs), nameof(VillagerPartyComponent.InitializationArgs.InitializeVillagerPartyProperties),
            new Type[] { typeof(MobileParty), typeof(Village) }),
        };

        return methods;
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
