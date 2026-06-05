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