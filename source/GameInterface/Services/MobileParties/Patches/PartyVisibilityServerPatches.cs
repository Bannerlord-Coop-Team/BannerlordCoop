using Common;
using HarmonyLib;
using System.Diagnostics;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>
/// Parties are always visible on server
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsSpotted))]
internal class PartyIsSpottedServerPatch
{
    private static void Postfix(ref bool __result)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(MobileParty))]
internal class PartyVisibilityOnServerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
    private static void PrefixIsVisible(ref bool value)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            value = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
    private static void PostfixIsVisible(ref bool __result)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            __result = true;
        }
    }

    [HarmonyPatch(nameof(MobileParty.IsInspected), MethodType.Setter)]
    private static void PrefixIsInspected(ref bool value)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            value = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
    private static void PostfixIsInspected(ref bool __result)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            __result = true;
        }
    }
}