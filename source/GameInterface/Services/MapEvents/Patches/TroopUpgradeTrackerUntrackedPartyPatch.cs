using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Returns 0 (nothing ready to upgrade) for an owner party the tracker never registered, instead of NREing.
/// Vanilla finds the owner's MapEventParty and reads mapEventParty.Troops; in a coop battle the host spawns AI
/// parties (siege defenders) added to the synced map event without a MapEventInvolvedPartiesAdded broadcast, so
/// Find returns null and the deployment tick dies on every such troop. A tracked party runs vanilla unchanged.
/// </summary>
[HarmonyPatch(typeof(TroopUpgradeTracker), "CalculateReadyToUpgradeSafe")]
internal class TroopUpgradeTrackerUntrackedPartyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TroopUpgradeTracker __instance, PartyBase owner, ref int __result)
    {
        if (!BattleSpawnGate.IsCoopBattleActive) return true;

        if (__instance._mapEventParties.Find(p => p.Party == owner) == null)
        {
            __result = 0;
            return false;
        }

        return true;
    }
}
