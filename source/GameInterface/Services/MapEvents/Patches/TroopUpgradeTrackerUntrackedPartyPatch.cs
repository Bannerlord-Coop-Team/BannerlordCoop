using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Returns 0 (nothing ready to upgrade) for an owner party the tracker never registered, instead of NREing.
/// Vanilla finds the owner's MapEventParty and reads mapEventParty.Troops, but synced map events can contain parties
/// missing from the local tracker's private list. A tracked party runs vanilla unchanged.
/// </summary>
[HarmonyPatch(typeof(TroopUpgradeTracker), "CalculateReadyToUpgradeSafe")]
internal class TroopUpgradeTrackerUntrackedPartyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TroopUpgradeTracker __instance, PartyBase owner, ref int __result)
    {
        if (__instance._mapEventParties.Find(p => p.Party == owner) == null)
        {
            __result = 0;
            return false;
        }

        return true;
    }
}
