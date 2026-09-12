using Common;
using Common.Util;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;

namespace GameInterface.Services.Tournaments.Patches;

/// <summary>
/// Keeps the native tournament alive until the authoritative completion transaction removes the coop session.
/// Completed-but-unrewarded sessions remain guarded so native lifecycle ticks cannot duplicate their rewards.
/// </summary>
[HarmonyPatch]
internal static class TournamentLifetimeGuardPatches
{
    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.ResolveTournament))]
    [HarmonyPrefix]
    static bool ResolveTournamentPrefix(Town town) => !HasOpenSession(town);

    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.RemoveTournament))]
    [HarmonyPrefix]
    static bool RemoveTournamentPrefix(TournamentGame game) => !HasOpenSession(game?.Town);

    [HarmonyPatch(typeof(TournamentGame), nameof(TournamentGame.UpdateTournamentPrize))]
    [HarmonyPrefix]
    static bool UpdateTournamentPrizePrefix(TournamentGame __instance) => !HasOpenSession(__instance?.Town);

    private static bool HasOpenSession(Town town)
    {
        if (ModInformation.IsClient || town == null ||
            !ContainerProvider.TryResolve(out IObjectManager objectManager) ||
            !objectManager.TryGetId(town, out var townId))
        {
            return false;
        }

        if (ContainerProvider.TryResolve(out ITournamentNativeRemovalAuthorization authorization) &&
            authorization.IsAuthorized(townId))
        {
            return false;
        }

        return ContainerProvider.TryResolve(out ITournamentSessionRegistry registry) &&
            registry.TryGetByTown(townId, out _);
    }
}
