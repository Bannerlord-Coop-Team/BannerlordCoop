using Common;
using Common.Messaging;
using GameInterface.Services.Tournaments.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;

namespace GameInterface.Services.Tournaments.Patches;

/// <summary>
/// Publishes a lightweight local invalidation after server-owned native tournament or leaderboard changes. The
/// lifetime-scoped state handler packs and broadcasts the authoritative collection.
/// </summary>
[HarmonyPatch]
internal static class TournamentNativeStatePatches
{
    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.AddTournament))]
    [HarmonyPostfix]
    static void AddTournamentPostfix() => PublishChanged();

    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.RemoveTournament))]
    [HarmonyPostfix]
    static void RemoveTournamentPostfix() => PublishChanged();

    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.AddLeaderboardEntry))]
    [HarmonyPostfix]
    static void AddLeaderboardEntryPostfix() => PublishChanged();

    [HarmonyPatch(typeof(TournamentManager), nameof(TournamentManager.DeleteLeaderboardEntry))]
    [HarmonyPostfix]
    static void DeleteLeaderboardEntryPostfix() => PublishChanged();

    [HarmonyPatch(typeof(TournamentGame), nameof(TournamentGame.UpdateTournamentPrize))]
    [HarmonyPrefix]
    static void UpdateTournamentPrizePrefix(TournamentGame __instance, out ItemObject __state)
    {
        __state = __instance?.Prize;
    }

    [HarmonyPatch(typeof(TournamentGame), nameof(TournamentGame.UpdateTournamentPrize))]
    [HarmonyPostfix]
    static void UpdateTournamentPrizePostfix(TournamentGame __instance, ItemObject __state)
    {
        if (!ReferenceEquals(__state, __instance?.Prize))
            PublishChanged();
    }

    private static void PublishChanged()
    {
        if (ModInformation.IsServer)
            MessageBroker.Instance.Publish(null, new TournamentNativeStateChanged());
    }
}
