using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

internal static class TournamentSpectatorSturgiaArenaLayout
{
    public static TournamentSpectatorSceneLayout Layout { get; } = new(
        new[]
        {
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(134.013f, 192.125f, 6.605f), -2.852f, new Vec3(12.478f, 1f, 4f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(123.574f, 187.962f, 6.605f), -2.614f, new Vec3(12.478f, 1f, 4f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(115.691f, 181.501f, 6.605f), -2.234f, new Vec3(9.358f, 1f, 4f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(107.39f, 180.667f, 6.605f), 2.694f, new Vec3(12.548f, 1f, 10.251f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(106.524f, 188.257f, 11.195f), 0.925f, new Vec3(16.219f, 1f, 5.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(118.041f, 197.976f, 11.195f), 0.456f, new Vec3(16.219f, 1f, 5.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(129.813f, 202.648f, 11.195f), 0.256f, new Vec3(16.219f, 1f, 5.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(156.597f, 209.833f, 11.058f), 0.256f, new Vec3(13.127f, 1f, 5.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(163.841f, 205.928f, 6.3f), -1.308f, new Vec3(13.127f, 1f, 10.756f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(159.499f, 198.392f, 6.3f), -2.905f, new Vec3(13.127f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(186.9f, 166.747f, 6.3f), 0.9f, new Vec3(9.296f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(179.342f, 160.745f, 6.3f), 0.489f, new Vec3(10.331f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(155.917f, 153.909f, 6.3f), 0.237f, new Vec3(38.89f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(158.469f, 142.939f, 11.355f), -2.903f, new Vec3(38.89f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(195.408f, 159.496f, 9.904f), -2.208f, new Vec3(13.825f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(195.236f, 167.329f, 6.652f), -0.529f, new Vec3(12.736f, 1f, 7.556f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(184.553f, 150.369f, 9.904f), -2.675f, new Vec3(18.655f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(132.376f, 137.985f, 11.355f), -3.093f, new Vec3(15.716f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(118.285f, 140.38f, 11.355f), 2.762f, new Vec3(15.716f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(122.024f, 150.629f, 6.267f), -0.335f, new Vec3(10.326f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(131.967f, 149.275f, 6.267f), 0.028f, new Vec3(10.326f, 1f, 4.267f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(114.443f, 147.832f, 6.267f), 0.999f, new Vec3(12.589f, 1f, 9.481f)),
        },
        new[]
        {
            new TournamentSpectatorSpawnData(1, new Vec3(142.742f, 198.805f, 6.221f), -2.901f),
            new TournamentSpectatorSpawnData(2, new Vec3(127.311f, 192.308f, 9.411f), -2.901f),
            new TournamentSpectatorSpawnData(3, new Vec3(135.799f, 146.471f, 9.411f), 0f),
            new TournamentSpectatorSpawnData(4, new Vec3(170.297f, 153.099f, 9.411f), 0.363f),
            new TournamentSpectatorSpawnData(5, new Vec3(158.116f, 202.209f, 9.411f), -2.935f),
        });
}
