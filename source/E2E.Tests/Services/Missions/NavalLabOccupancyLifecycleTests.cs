#if DEBUG
using Common.Network;
using E2E.Tests.Environment.Instance;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabOccupancyLifecycleTests : NavalMissionTestEnvironment
{
    public NavalLabOccupancyLifecycleTests(ITestOutputHelper output) : base(output) { }

    private void StartNative()
    {
        CreateLab(NavalLabMode.TwoClientNative);
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        Execute("complete-deployment"); Tick(First); Tick(Second);
    }

    private EnvironmentInstance Owner(int ship) => ship == 0 ? First : Second;
    private EnvironmentInstance Other(int ship) => ship == 0 ? Second : First;

    private NetworkNavalLabStations Release(int ship, long revision, params bool[] released) =>
        Adapter(First).Stations[ship].WithRelease(revision, released);

    private void PublishOwnerRelease(NetworkNavalLabStations release)
    {
        var owner = Owner(release.Ship);
        owner.Call(() => Adapter(owner).SendStationRelease!(release));
        PumpAll();
    }

    private void SendRaw(EnvironmentInstance sender, NetworkNavalLabStations release)
    {
        sender.Call(() => sender.Resolve<INetwork>().SendAll(release));
        PumpAll();
    }

    private static bool Held(EnvironmentInstance client) => Actions(client).Any(action => action.Kind == "hold");

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OwnerRelease_IsReplayedOnceOnTheOtherOwner_InRevisionOrder(int ship)
    {
        StartNative();

        PublishOwnerRelease(Release(ship, 1, true, false, false, false));
        PublishOwnerRelease(Release(ship, 2, true, true, false, false));

        Assert.Equal(new long[] { 1, 2 }, Adapter(Other(ship)).StationReleases.Select(release => release.Revision));
        Assert.All(Adapter(Other(ship)).StationReleases, release => Assert.Equal(ship, release.Ship));
        Assert.Equal(new[] { true, true, false, false }, Adapter(Other(ship)).StationReleases[1].Released);
        Assert.Empty(Adapter(Owner(ship)).StationReleases);
        Assert.False(Held(First) || Held(Second));
    }

    [Theory]
    [InlineData("not_original_owner")]
    [InlineData("skipped_revision")]
    [InlineData("re_occupied")]
    [InlineData("foreign_combatant")]
    public void InvalidRelease_StillHoldsTheFixture(string condition)
    {
        StartNative();
        if (condition == "re_occupied") PublishOwnerRelease(Release(0, 1, true, false, false, false));
        var commit = Adapter(First).Stations[0];
        var release = condition switch
        {
            "skipped_revision" => Release(0, 2, true, false, false, false),
            "re_occupied" => Release(0, 2, false, true, false, false),
            "foreign_combatant" => new NetworkNavalLabStations(commit.IncarnationId, 1, 0, "commit",
                Manifest.Combatants.Skip(6).Take(4).ToArray(), commit.Keys, commit.SailKeys, commit.OarKeys, commit.OarSides)
                .WithRelease(1, new[] { true, false, false, false }),
            _ => Release(0, 1, true, false, false, false)
        };

        SendRaw(condition == "not_original_owner" ? Second : First, release);

        Assert.True(Held(First) && Held(Second));
        Assert.Equal(condition == "re_occupied" ? 1 : 0, Adapter(Second).StationReleases.Count);
    }

    // Either owner may fault first; the rope owner's frozen state still reaches the other owner after the sample stream closed.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FinalRopeLifecycle_ReachesTheOtherOwner_AfterTheSampleStreamClosed(bool ropeOwnerFaults)
    {
        StartNative();
        Adapter(First).FinalRopes = new NetworkNavalLabRopeFinal(Manifest.IncarnationId, 0, Manifest.Ships[0], new[]
        {
            new NetworkNavalLabRopeState
            {
                SourceStation = 5, SourceKey = "source-5", TargetStation = 24, TargetKey = "target-24", Generation = 1,
                State = 3, Length = 3, HookFrame = NetworkNavalLabOarPresentation.FromFrame(MatrixFrame.Identity),
                History = new[] { 0, 1, 2, 3 }, PlankFlight = new float[] { 1, 0, 1, 1, 1, 0, 0, 0 }, DecorationPlanks = 10
            }
        });
        Adapter(ropeOwnerFaults ? First : Second).Blocker = "simulated_fault";

        Tick(ropeOwnerFaults ? First : Second); Tick(First); Tick(Second);
        int samplesBefore = Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count();
        SendShipSample(First, Adapter(First).ShipSample(10_000, 10_000, MatrixFrame.Identity));

        Assert.True(Held(First) && Held(Second));
        Assert.Equal(samplesBefore, Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>().Count());
        var accepted = Assert.Single(Adapter(Second).AcceptedFinalRopes);
        Assert.Equal(3, Assert.Single(accepted.Ropes).State);
        Assert.Empty(Adapter(First).AcceptedFinalRopes);
    }
}
#endif
