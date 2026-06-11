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
    //[HarmonyPostfix]
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
    private static void PostfixIsVisible(MobileParty __instance, ref bool __result)
    {
        if (ModInformation.IsServer || Debugger.IsAttached)
        {
            // Return is active (inactive is player captivity)
            __result = __instance.IsActive;

            if (__result != __instance._isVisible)
            {
                __instance.Party.OnVisibilityChanged(__result);
                __instance.Party.SetVisualAsDirty();
            }
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
}