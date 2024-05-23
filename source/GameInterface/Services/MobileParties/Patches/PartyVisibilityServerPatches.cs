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
    private static void Postfix(ref MobileParty __instance, ref bool __result)
    {
        if (ModInformation.IsServer)
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(MobileParty))]
internal class PartyVisibilityOnServerPatch
{
    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
    private static void Prefix(ref bool value)
    {
        if (ModInformation.IsServer)
        {
            value = true;
        }
    }

    [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
    private static void Postfix(ref bool __result)
    {
        if (ModInformation.IsServer)
        {
            __result = true;
        }
    }
}