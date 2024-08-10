using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Tests.Bootstrap.Patches.PartyComponents;

[HarmonyPatch]
internal class MilitiaPartyComponentPatches
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(MilitiaPartyComponent)))
        {
            if (method.Name == nameof(MilitiaPartyComponent.InitializeMilitiaPartyProperties))
            {
                yield return method;
            }
        }
    }

    [HarmonyPrefix]
    private static bool Prefix() => false;
}
