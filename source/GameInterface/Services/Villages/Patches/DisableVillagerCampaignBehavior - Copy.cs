using HarmonyLib;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch("VillageTradeBoundCampaignBehavior", "RegisterEvents")]
internal class DisableVillageTradeBoundCampaignBehavior
{
    static bool Prefix() => false;
}
