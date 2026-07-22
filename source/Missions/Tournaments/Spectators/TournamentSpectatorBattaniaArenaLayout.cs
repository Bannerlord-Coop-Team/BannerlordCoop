using System;
using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

internal static class TournamentSpectatorBattaniaArenaLayout
{
    public static TournamentSpectatorSceneLayout Layout { get; } = new(
        Array.Empty<TournamentSpectatorBarrierData>(),
        new[]
        {
            new TournamentSpectatorSpawnData(1, new Vec3(183.988f, 123.73f, 0f), 1.604f),
        });
}
