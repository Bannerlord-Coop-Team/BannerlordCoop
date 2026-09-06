using GameInterface.Services.Tournaments.UI;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;

namespace GameInterface.Services.Tournaments.Patches;

internal static class TournamentTabInputPatches
{
    internal static bool IsCoopTournamentMissionActive()
    {
        return Mission.Current != null &&
            ContainerProvider.TryResolve<TournamentMissionUIContext>(out var context) &&
            context.TryGet(out _);
    }
}

[HarmonyPatch(typeof(MissionGauntletBattleScore), nameof(MissionGauntletBattleScore.OnMissionScreenTick))]
internal static class TournamentScoreboardInputPatches
{
    [HarmonyPrefix]
    private static bool SuppressTournamentScoreboard()
        => ShouldRunScoreboardTick(TournamentTabInputPatches.IsCoopTournamentMissionActive());

    internal static bool ShouldRunScoreboardTick(bool isCoopTournamentMissionActive)
        => !isCoopTournamentMissionActive;
}
