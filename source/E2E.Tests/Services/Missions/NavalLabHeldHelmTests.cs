#if DEBUG
using GameInterface.Services.MapEvents;
using Missions.Battles;
using Missions.Messages;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabHeldHelmTests : NavalMissionTestEnvironment
{
    public NavalLabHeldHelmTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void HeldMode_RequiresBothOriginalOwnersAndNeverRequestsActivationOrFrames()
    {
        var create = CreateLab(NavalLabMode.HeldHelm);
        Server.Call(() => Server.Resolve<INavalLabCoordinator>().Create(create, "naval-A", "naval-B", NavalLabMode.HeldHelm));
        PumpAll();
        Assert.All(Clients, client => Assert.Equal(1, Adapter(client).OpenCount));
        Assert.Equal(NavalLabMode.HeldHelm, Manifest.Mode);
        Ready(First);
        Assert.Throws<InvalidOperationException>(() => Execute("take-helm"));
        Tick(First);
        Assert.Empty(Adapter(First).HeldHelmCalls);
        Ready(Second);
        Tick(First);
        Tick(Second);
        Assert.All(Clients, client =>
        {
            Assert.Equal(NavalLabMode.HeldHelm, Adapter(client).OpenedManifest!.Mode);
            Assert.DoesNotContain("authority:True", Adapter(client).Calls);
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkNavalLabFrames>());
            Assert.Equal(0, Adapter(client).ApplyCount);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TakeRelease_RoutesOnlyToOwnerAndDeduplicatesRequests(int ship)
    {
        StartReleased(NavalLabMode.HeldHelm);
        var owner = ship == 0 ? First : Second;
        var other = ship == 0 ? Second : First;
        Guid take = Execute("take-helm", ship);
        Assert.Equal("taken", Receipt(owner, take));
        Execute("take-helm", ship, operation: take);
        Assert.Single(Adapter(owner).HeldHelmCalls);
        Assert.Empty(Adapter(other).HeldHelmCalls);
        Assert.Equal("already_taken", Receipt(owner, Execute("take-helm", ship)));
        Assert.Equal("released", Receipt(owner, Execute("release-helm", ship)));
        Assert.Equal("already_released", Receipt(owner, Execute("release-helm", ship)));
        Assert.All(Adapter(owner).HeldHelmCalls, call => Assert.Equal(ship, call.ship));
        Execute("take-helm", ship);
        Execute("stop");
        Assert.False(Adapter(owner).HeldHelmTaken);
    }

    [Fact]
    public void WrongOwnerExpiredAndInvalidActions_DoNotReachAdapter()
    {
        StartReleased(NavalLabMode.HeldHelm);
        var wrong = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1,
            "take-helm", 1, 0, false, DateTime.UtcNow.AddSeconds(30).Ticks);
        SendAction(First, wrong);
        Assert.Equal("rejected:not_original_owner", Receipt(First, wrong.OperationId));
        var expired = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1,
            "take-helm", 0, 0, false, DateTime.UtcNow.AddSeconds(-1).Ticks);
        SendAction(First, expired);
        Assert.Equal("rejected:expired_control", Receipt(First, expired.OperationId));
        Assert.Throws<InvalidOperationException>(() => Execute("helm"));
        Assert.Throws<InvalidOperationException>(() => Execute("walk"));
        Assert.Throws<ArgumentException>(() => Execute("take-helm", value: 1));
        Assert.Empty(Adapter(First).HeldHelmCalls);
        Assert.Empty(Adapter(Second).HeldHelmCalls);
    }

    [Fact]
    public void HoldAndOwnerDeparture_ReleaseHeldUseAndPreventFurtherTake()
    {
        StartReleased(NavalLabMode.HeldHelm);
        Execute("take-helm");
        var hold = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 0, "hold", 0, 0, false);
        SendAction(First, hold);
        Assert.False(Adapter(First).HeldHelmTaken);
        var take = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1,
            "take-helm", 0, 0, false, DateTime.UtcNow.AddSeconds(30).Ticks);
        SendAction(First, take);
        Assert.Equal("rejected:not_released_or_owner_departed", Receipt(First, take.OperationId));
        Execute("take-helm", 1);
        Second.Resolve<IBattleHostRegistry>().Set(Manifest.InstanceId, new BattleHostAssignment("naval-B", Array.Empty<string>(), 2));
        Tick(Second);
        Assert.False(Adapter(Second).HeldHelmTaken);
        Assert.DoesNotContain("authority:True", Adapter(Second).Calls);
    }

    [Fact]
    public void DefaultMode_RejectsHeldUseAndRetainsNormalActivation()
    {
        StartReleased();
        Assert.Equal(NavalLabMode.Activation, Manifest.Mode);
        Assert.Throws<InvalidOperationException>(() => Execute("take-helm"));
        Tick(First);
        Tick(Second);
        Assert.Single(Clients.Where(client => Adapter(client).Authority));
        Assert.All(Clients, client => Assert.Empty(Adapter(client).HeldHelmCalls));
    }
}
#endif
