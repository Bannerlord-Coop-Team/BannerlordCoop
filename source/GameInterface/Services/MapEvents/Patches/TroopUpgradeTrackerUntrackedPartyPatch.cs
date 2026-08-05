using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Returns 0 (nothing ready to upgrade) instead of NREing whenever the state vanilla assumes is not there yet.
/// A fully tracked party with a synced roster runs vanilla unchanged.
/// </summary>
/// <remarks>
/// Vanilla walks <c>_mapEventParties.Find(p =&gt; p.Party == owner).Troops</c> and enumerates it, with no null
/// check on either step. Both are reachable on a client:
///
/// - the owner may be missing from the local tracker entirely — a synced map event can contain parties this
///   client never registered, which is the case this patch originally covered;
/// - the owner may be tracked while its <c>Troops</c> is still null, because coop fills
///   <c>MapEventParty._roster</c> from NetworkUpdateMapEventParty and that message can land after the party
///   is already in the battle.
///
/// The second one is not cosmetic. This runs under BattleAgentLogic.OnAgentBuild during agent spawn, so the
/// NRE escapes into Mission.OnTick and kills the tick: DeploymentMissionController.SetupTeams never
/// finishes and the joining player sits on the deployment screen with no Ready prompt. It only catches
/// whoever enters the battle before their roster sync lands, which is why joining in one order worked and
/// the other did not.
/// </remarks>
[HarmonyPatch(typeof(TroopUpgradeTracker), "CalculateReadyToUpgradeSafe")]
internal class TroopUpgradeTrackerUntrackedPartyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(TroopUpgradeTracker __instance, PartyBase owner, ref int __result)
    {
        if (HasTrackedTroops(__instance, owner)) return true;

        __result = 0;
        return false;
    }

    private static bool HasTrackedTroops(TroopUpgradeTracker tracker, PartyBase owner)
    {
        // Null on an instance the registry built through its SkipConstructor path before OnClientCreated
        // populated it — the tracker's constructor does nothing but allocate these collections.
        var parties = tracker._mapEventParties;
        if (parties == null) return false;

        // Troops is MapEventParty._roster, which arrives over the wire separately from the party itself.
        return parties.Find(p => p?.Party == owner)?.Troops != null;
    }
}
