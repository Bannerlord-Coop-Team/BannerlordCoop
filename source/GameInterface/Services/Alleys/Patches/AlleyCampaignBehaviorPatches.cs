using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Controls which parts of the vanilla <see cref="AlleyCampaignBehavior"/> run in co-op by gating its
/// individual event handlers, so vanilla <c>RegisterEvents</c> still wires every listener and each
/// handler decides per-role whether to run. The host is a dedicated server with no main hero, and the
/// behavior derefs <c>Hero.MainHero</c> throughout (menus, daily ticks, fights), so only the co-op-safe
/// UI runs, and only on the client: <c>OnSessionLaunched</c> (the manage-alley menus and dialogs) and
/// <c>LocationCharactersAreReadyToSpawn</c> (the alley scene NPCs). The AI-churn and authoritative-
/// ownership handlers (daily ticks, new-game seeding, occupied/cleared/owner-changed, hero-killed, the
/// alley-leader and can-die restrictions) stay off on both sides, so alley ownership can only change
/// server-authoritatively (see AlleyHandler / AlleyManagementHandler), never from divergent per-client RNG.
/// </summary>
[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class AlleyCampaignBehaviorPatches
{
    // Client-only: wires the manage-alley game menus and dialogs.
    [HarmonyPatch("OnSessionLaunched")]
    [HarmonyPrefix]
    private static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    // Client-only: populates the alley scene with its NPCs when the owner visits.
    [HarmonyPatch("LocationCharactersAreReadyToSpawn")]
    [HarmonyPrefix]
    private static bool LocationCharactersAreReadyToSpawnPrefix() => ModInformation.IsClient;

    // The handlers below are AI churn or authoritative ownership changes. Ownership is driven
    // server-authoritatively (AlleyHandler / AlleyManagementHandler), so these stay off on both sides.
    [HarmonyPatch("OnHeroKilled")]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix() => false;

    [HarmonyPatch("OnNewGameCreated")]
    [HarmonyPrefix]
    private static bool OnNewGameCreatedPrefix() => false;

    [HarmonyPatch("OnAlleyOccupiedByPlayer")]
    [HarmonyPrefix]
    private static bool OnAlleyOccupiedByPlayerPrefix() => false;

    [HarmonyPatch("OnAlleyClearedByPlayer")]
    [HarmonyPrefix]
    private static bool OnAlleyClearedByPlayerPrefix() => false;

    [HarmonyPatch("OnAlleyOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnAlleyOwnerChangedPrefix() => false;

    [HarmonyPatch("DailyTickSettlement")]
    [HarmonyPrefix]
    private static bool DailyTickSettlementPrefix() => false;

    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix() => false;

    [HarmonyPatch("CommonAlleyLeaderRestriction")]
    [HarmonyPrefix]
    private static bool CommonAlleyLeaderRestrictionPrefix() => false;

    [HarmonyPatch("OnAfterMissionStarted")]
    [HarmonyPrefix]
    private static bool OnAfterMissionStartedPrefix() => false;

    [HarmonyPatch("CanHeroDie")]
    [HarmonyPrefix]
    private static bool CanHeroDiePrefix() => false;
}
