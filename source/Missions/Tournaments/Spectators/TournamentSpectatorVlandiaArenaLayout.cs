using TaleWorlds.Library;

namespace Missions.Tournaments.Spectators;

internal static class TournamentSpectatorVlandiaArenaLayout
{
    public static TournamentSpectatorSceneLayout Layout { get; } = new(
        new[]
        {
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(177.89f, 109.346f, 6.867f), 1.573f, new Vec3(16f, 1f, 4f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(173.886f, 94.413f, 6.867f), 1.047f, new Vec3(16f, 1f, 4f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(173.927f, 83.885f, 6.867f), 2.413f, new Vec3(11.644f, 1f, 8.433f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(171.121f, 80.739f, 6.867f), 2.413f, new Vec3(11.644f, 1f, 8.433f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(131.253f, 120.138f, 6.867f), -1.494f, new Vec3(36.837f, 1f, 4.273f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(130.311f, 143.026f, 6.867f), -1.67f, new Vec3(10.02f, 1f, 4.273f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(133.927f, 151.669f, 6.867f), -2.228f, new Vec3(10.02f, 1f, 4.273f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(134.846f, 159.019f, 6.867f), -0.877f, new Vec3(10.02f, 1f, 8.065f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(127.752f, 100.563f, 6.867f), -3.03f, new Vec3(11.121f, 1f, 7.99f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(120.879f, 119.706f, 6.867f), 1.668f, new Vec3(36.951f, 1f, 7.99f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(120.308f, 145.054f, 6.867f), 1.405f, new Vec3(17.418f, 1f, 7.99f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(126.936f, 158.162f, 6.867f), 0.758f, new Vec3(17.418f, 1f, 7.99f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(172.601f, 154.764f, 6.867f), 2.35f, new Vec3(11.294f, 1f, 4.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(177.459f, 145.55f, 6.867f), 1.745f, new Vec3(11.294f, 1f, 4.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(177.995f, 136.516f, 6.867f), 1.613f, new Vec3(11.294f, 1f, 4.644f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(183.772f, 131.207f, 6.867f), -3.085f, new Vec3(11.294f, 1f, 8.823f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(188.498f, 136.992f, 6.867f), -1.574f, new Vec3(11.294f, 1f, 8.823f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(187.822f, 147.631f, 6.867f), -1.462f, new Vec3(11.294f, 1f, 8.823f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(185.1f, 155.888f, 6.867f), -1.077f, new Vec3(11.294f, 1f, 8.823f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(178.507f, 163.553f, 6.867f), -0.732f, new Vec3(11.294f, 1f, 8.823f)),
            new TournamentSpectatorBarrierData("_barrier_16x04m", new Vec3(171.261f, 163.036f, 5.563f), 1.018f, new Vec3(11.294f, 1f, 8.823f)),
        },
        new[]
        {
            new TournamentSpectatorSpawnData(1, new Vec3(179.724f, 126.016f, 7.088f), 1.652f),
            new TournamentSpectatorSpawnData(2, new Vec3(179.428f, 143.358f, 8.051f), 1.652f),
            new TournamentSpectatorSpawnData(3, new Vec3(129.192f, 148.895f, 8.051f), -2.084f),
            new TournamentSpectatorSpawnData(4, new Vec3(129.908f, 113.577f, 8.051f), -1.43f),
            new TournamentSpectatorSpawnData(5, new Vec3(177.494f, 95.507f, 8.051f), 1.041f),
            new TournamentSpectatorSpawnData(6, new Vec3(177.974f, 94.879f, 8.051f), 1.041f),
        });
}
