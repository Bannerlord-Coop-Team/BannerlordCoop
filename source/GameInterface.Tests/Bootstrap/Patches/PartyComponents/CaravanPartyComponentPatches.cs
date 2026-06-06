using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;

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
