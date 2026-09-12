using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

internal static class TournamentSpectatorKhuzaitArenaLayout
{
    public static TournamentSpectatorSceneLayout Layout { get; } = new(
        new[]
        {
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(283.794f, 303.363f, 3.975f), 1.294f, new Vec3(16f, 1f, 8.736f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(289.887f, 310.003f, 3.975f), -0.27f, new Vec3(8.93f, 1f, 9.629f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(285.785f, 294.736f, 3.975f), 2.912f, new Vec3(8.93f, 1f, 9.629f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(292.701f, 300.93f, 3.975f), -1.861f, new Vec3(15.523f, 1f, 9.629f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(253.914f, 346.164f, 3.35f), -1.737f, new Vec3(16f, 1f, 8.736f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(239.422f, 278.227f, 5.239f), -0.204f, new Vec3(9.346f, 1f, 4.42f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(237.979f, 270.638f, 5.239f), 2.912f, new Vec3(9.346f, 1f, 5.88f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(234.871f, 275.006f, 5.239f), 1.398f, new Vec3(9.346f, 1f, 5.88f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(242.918f, 273.444f, 5.239f), -1.782f, new Vec3(9.346f, 1f, 5.88f)),
        },
        new[]
        {
            new TournamentSpectatorSpawnData(1, new Vec3(287.454f, 306.033f, 5.156f), 1.394f),
            new TournamentSpectatorSpawnData(2, new Vec3(240.828f, 274.306f, 6.629f), -0.134f),
            new TournamentSpectatorSpawnData(3, new Vec3(247.977f, 343.299f, 4.424f), 2.968f),
        });
}
