using Common;
using Common.Messaging;
using GameInterface.Services.Alleys.Messages;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Alleys.Patches;

/// <summary>
/// Controls which parts of the vanilla <see cref="AlleyCampaignBehavior"/> run in co-op by gating its
/// individual event handlers, so vanilla <c>RegisterEvents</c> still wires every listener and each
/// handler decides per-role whether to run. The host is a dedicated server with no main hero and the
/// behavior derefs <c>Hero.MainHero</c> throughout, so the client runs the co-op-safe handlers (the
/// manage-alley menus/dialogs, the alley scene NPCs, and the per-player UX: the lost-ownership popup,
/// the don't-die-in-an-alley-fight guard, and the alley-leader-can't-lead-a-party restriction). The AI
/// activity (daily ticks, hero-killed) can't run per client - the host's <c>_playerOwnedCommonAreaData</c>
/// is empty and RNG would diverge - so those handlers publish a trigger the server-side AlleyHandler
/// applies authoritatively; new-game gang seeding runs on the server only; and the occupied/cleared
/// handlers stay off (the take-over is already driven through AlleyManagementHandler).
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

    // Stay off: the player take-over/clear is already driven authoritatively by the RequestAcquireAlley /
    // RequestAbandonAlley path (AlleyManagementHandler), so the vanilla completion must not also run.
    [HarmonyPatch("OnAlleyOccupiedByPlayer")]
    [HarmonyPrefix]
    private static bool OnAlleyOccupiedByPlayerPrefix() => false;

    [HarmonyPatch("OnAlleyClearedByPlayer")]
    [HarmonyPrefix]
    private static bool OnAlleyClearedByPlayerPrefix() => false;

    // AI activity: on the server, hand off to AlleyHandler (RNG rolled once, results replicated) and
    // skip the vanilla body on both sides (host list is empty, per-client RNG would diverge).
    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix(AlleyCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) MessageBroker.Instance.Publish(__instance, new AlleyDailyTickTriggered());
        return false;
    }

    [HarmonyPatch("DailyTickSettlement")]
    [HarmonyPrefix]
    private static bool DailyTickSettlementPrefix(AlleyCampaignBehavior __instance, Settlement settlement)
    {
        if (ModInformation.IsServer) MessageBroker.Instance.Publish(__instance, new AlleyDailyTickSettlementTriggered(settlement));
        return false;
    }

    [HarmonyPatch("OnHeroKilled")]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix(AlleyCampaignBehavior __instance, Hero victim)
    {
        if (ModInformation.IsServer) MessageBroker.Instance.Publish(__instance, new AlleyHeroKilledTriggered(victim));
        return false;
    }

    // Seeds initial gang alley ownership at new-game on the server only; its SetOwner calls replicate and
    // persist for joining clients, and this path never derefs Hero.MainHero.
    [HarmonyPatch("OnNewGameCreated")]
    [HarmonyPrefix]
    private static bool OnNewGameCreatedPrefix() => ModInformation.IsServer;
}
