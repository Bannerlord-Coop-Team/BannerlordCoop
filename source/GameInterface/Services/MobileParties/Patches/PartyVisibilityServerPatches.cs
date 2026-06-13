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
    internal static void Postfix(MobileParty __instance, ref bool __result)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            // Match the IsVisible patch below: only live parties are force-spotted. A destroyed
            // party must read as unspotted, or the map nameplate machinery keeps re-creating its
            // banner (with 0 troops) after the destruction replicates.
            __result = __instance.IsActive;
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