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
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
    [HarmonyPrefix]
    private static void PrefixIsVisible(MobileParty __instance, ref bool value)
    {
        if (!(ModInformation.IsServer || Debugger.IsAttached)) return;

        value = __instance.IsActive;
    }

    [HarmonyPatch(nameof(MobileParty.IsInspected), MethodType.Setter)]
    [HarmonyPrefix]
    private static void PrefixIsInspected(ref bool value)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            value = true;
        }
    }
}