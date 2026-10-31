using Common;
using HarmonyLib;
using NavalDLC.CampaignBehaviors;

namespace Coop.Naval.Storms.Patches;

[HarmonyPatch(typeof(NavalStormriderCampaignBehaviour))]
internal class NavalStormriderCampaignBehaviourPatches
{
    // Grants Stormrider xp to every party with the perk except MobileParty.MainParty, which the server never has
    [HarmonyPatch(nameof(NavalStormriderCampaignBehaviour.OnHourlyTick))]
    [HarmonyPrefix]
    public static bool OnHourlyTickPrefix() => ModInformation.IsServer;

    // Main party only path, the hourly tick on the server already covers player parties
    [HarmonyPatch(nameof(NavalStormriderCampaignBehaviour.TickEvent))]
    [HarmonyPrefix]
    public static bool TickEventPrefix() => false;

    [HarmonyPatch(nameof(NavalStormriderCampaignBehaviour.OnMobilePartyDestroyed))]
    [HarmonyPrefix]
    public static bool OnMobilePartyDestroyedPrefix() => ModInformation.IsServer;
}
