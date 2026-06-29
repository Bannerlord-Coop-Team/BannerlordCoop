using Common;
using HarmonyLib;
using SandBox.CampaignBehaviors;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Controls which parts of the vanilla <see cref="AlleyCampaignBehavior"/> run in co-op by gating its
/// individual event handlers, so vanilla <c>RegisterEvents</c> still wires every listener and each
/// handler decides per-role whether to run. The host is a dedicated server with no main hero and the
/// behavior derefs <c>Hero.MainHero</c> throughout, so the client runs the co-op-safe handlers (the
/// manage-alley menus/dialogs, the alley scene NPCs, and the per-player UX: the lost-ownership popup,
/// the don't-die-in-an-alley-fight guard, and the alley-leader-can't-lead-a-party restriction), while
/// the AI-churn and authoritative-ownership handlers (daily ticks, new-game seeding, occupied/cleared,
/// hero-killed) stay off on both sides so alley ownership can only change server-authoritatively (see
/// AlleyHandler / AlleyManagementHandler), never from divergent per-client RNG.
/// </summary>
[HarmonyPatch(typeof(AlleyCampaignBehavior))]
internal class AlleyCampaignBehaviorPatches
{
    // Client-only. The host has no main hero, so these are either client UI/scene wiring or guard on
    // Hero.MainHero (no-ops on the server); only the client should run them.

    // Wires the manage-alley game menus and dialogs.
    [HarmonyPatch("OnSessionLaunched")]
    [HarmonyPrefix]
    private static bool OnSessionLaunchedPrefix() => ModInformation.IsClient;

    // Populates the alley scene with its NPCs when the owner visits.
    [HarmonyPatch("LocationCharactersAreReadyToSpawn")]
    [HarmonyPrefix]
    private static bool LocationCharactersAreReadyToSpawnPrefix() => ModInformation.IsClient;

    // Shows the "you have lost the ownership of the alley" popup (guards on oldOwner == Hero.MainHero).
    [HarmonyPatch("OnAlleyOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnAlleyOwnerChangedPrefix() => ModInformation.IsClient;

    // Keeps the player from dying during an alley fight (guards on Hero.MainHero + the alley-fight flag).
    [HarmonyPatch("CanHeroDie")]
    [HarmonyPrefix]
    private static bool CanHeroDiePrefix() => ModInformation.IsClient;

    // Resets the alley-fight flag CanHeroDie reads.
    [HarmonyPatch("OnAfterMissionStarted")]
    [HarmonyPrefix]
    private static bool OnAfterMissionStartedPrefix() => ModInformation.IsClient;

    // Stops a hero assigned to one of the local player's alleys from also leading a party / being a governor.
    [HarmonyPatch("CommonAlleyLeaderRestriction")]
    [HarmonyPrefix]
    private static bool CommonAlleyLeaderRestrictionPrefix() => ModInformation.IsClient;

    // Off on both sides. These are AI churn or authoritative ownership changes (they use RNG and mutate
    // alley world-state). Player ownership is driven server-authoritatively (AlleyHandler /
    // AlleyManagementHandler) and AI alley churn is not replicated, so running them anywhere would only
    // create divergent per-client state.
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

    [HarmonyPatch("DailyTickSettlement")]
    [HarmonyPrefix]
    private static bool DailyTickSettlementPrefix() => false;

    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix() => false;
}
