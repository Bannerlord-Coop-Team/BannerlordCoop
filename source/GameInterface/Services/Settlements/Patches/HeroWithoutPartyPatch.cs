using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Patches for AddHeroWithoutParty() => Server side sync
/// RemoveHeroWithoutParty() => Server side sync
/// </summary>
[HarmonyPatch(typeof(Settlement))]
public class HeroWithoutPartyPatch
{
    // TODO: discuss and test if needed, not sure...
    // only server needs to know about this..
    // may not be needed but for now if it does the code is here.
    [HarmonyPatch("AddHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool AddHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual) => ModInformation.IsServer;


    [HarmonyPatch("RemoveHeroWithoutParty")]
    [HarmonyPrefix]
    private static bool RemoveHeroWithoutPartyPrefix(ref Settlement __instance, Hero individual) => ModInformation.IsServer;
}
