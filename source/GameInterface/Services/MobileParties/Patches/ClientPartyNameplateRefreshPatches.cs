using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Patches;

/// <summary>Refreshes client map nameplates when a replicated party is parked or reactivated.</summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.IsActive), MethodType.Setter)]
internal class ClientPartyNameplateRefreshPatches
{
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, out bool __state)
    {
        __state = __instance.IsActive;
    }

    [HarmonyPostfix]
    private static void Postfix(MobileParty __instance, bool __state)
    {
        if (ModInformation.IsClient && __state != __instance.IsActive && __instance.Party != null)
            CampaignEventDispatcher.Instance?.OnPartyVisibilityChanged(__instance.Party);
    }
}
