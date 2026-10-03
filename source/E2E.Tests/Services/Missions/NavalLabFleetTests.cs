#if DEBUG
using Common.Network;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public sealed class NavalLabFleetTests : NavalMissionTestEnvironment
{
    public NavalLabFleetTests(ITestOutputHelper output) : base(output) { }

    private void StartFleet()
    {
        CreateLab(NavalLabMode.TwoClientNative, hullsPerParticipant: 2);
        foreach (var client in Clients) Adapter(client).CaptureShipSamples = false;
        Ready(First); Ready(Second); Tick(First); Tick(Second);
        Execute("complete-deployment"); Tick(First); Tick(Second);
    }

    [Fact]
    public void CreateWithTwoHullsPerParticipant_AssignsSecondaryHullsAfterFlagships()
    {
        StartFleet();
        Assert.Equal(4, Manifest.Ships.Length);
        Assert.Equal(new[] { 0, 1, 0, 1 }, Manifest.ShipOwners);
        Assert.Equal(10, Manifest.Combatants.Length);
        Assert.Equal(4, Adapter(First).OpenedManifest!.Ships.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OwnerSendsSampleForEveryOwnedHull_AndForeignClientWritesEachSlot(int owner)
    {
        StartFleet();
        var source = owner == 0 ? First : Second;
        var other = owner == 0 ? Second : First;
        int secondary = owner + 2;
        Adapter(source).Frames[owner] = new MatrixFrame(Mat3.Identity, new Vec3(11, 0, 0));
        Adapter(source).Frames[secondary] = new MatrixFrame(Mat3.Identity, new Vec3(22, 0, 0));
        Adapter(source).CaptureShipSamples = true;
        Tick(source);
        Assert.Equal(new[] { owner, secondary }, Adapter(source).CapturedShips.Select(sample => sample.Slot));
        Assert.Null(Adapter(source).CapturedShips[1].Presentation);
        var received = other.InternalMessages.GetMessages<NetworkNavalLabShipSample>().ToArray();
        Assert.Equal(new[] { owner, secondary }, received.Select(sample => sample.Slot));
        Assert.All(received, sample => Assert.Equal(Manifest.Controllers[owner], sample.OriginalOwner));
        Assert.Empty(source.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Tick(other);
        Assert.Equal(new[] { owner, secondary }, Adapter(other).ForeignFrameWrites.Select(write => write.slot).OrderBy(slot => slot));
        Assert.Equal(22, Adapter(other).Frames[secondary].origin.x);
        Assert.Equal("owner", (string?)ShipStream(source, secondary)["localRole"]);
        Assert.Equal("foreign", (string?)ShipStream(other, secondary)["localRole"]);
        Assert.Equal(1, (long)ShipStream(other, secondary)["appliedSequence"]!);
    }

    [Theory]
    [InlineData("sender")]
    [InlineData("presentation")]
    public void ServerRejectsSecondarySampleFromWrongOwnerOrWithPresentation(string kind)
    {
        StartFleet();
        var sample = kind == "sender"
            ? Adapter(Second).ShipSample(2, 5, 50, MatrixFrame.Identity)
            : Adapter(First).ShipSample(2, 5, 50, MatrixFrame.Identity);
        if (kind == "presentation")
            typeof(NetworkNavalLabShipSample).GetField("Presentation")!.SetValue(sample,
                Adapter(First).ShipSample(0, 5, 50, MatrixFrame.Identity).Presentation);
        SendShipSample(kind == "sender" ? Second : First, sample);
        Assert.Empty(First.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Empty(Second.InternalMessages.GetMessages<NetworkNavalLabShipSample>());
        Assert.Equal(0, (long)ShipStream(Second, 2)["acceptedSequence"]!);
    }

    [Theory]
    [InlineData("fleet-follow", 2, true)]
    [InlineData("fleet-stop", 2, false)]
    [InlineData("fleet-follow", 3, true)]
    [InlineData("fleet-stop", 3, false)]
    public void FleetOrdersRouteOnlyToTheSecondaryHullOwner(string kind, int ship, bool follow)
    {
        StartFleet();
        var owner = ship == 2 ? First : Second;
        var other = ship == 2 ? Second : First;
        var operation = Execute(kind, ship);
        Assert.Equal((ship, follow), Assert.Single(Adapter(owner).FleetOrders));
        Assert.Empty(Adapter(other).FleetOrders);
        Assert.DoesNotContain(Actions(other), action => action.OperationId == operation);
        Assert.Single(Actions(owner), action => action.OperationId == operation);
        Assert.Equal("applied:simulated_fleet_order", Receipt(owner, operation));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ServerRejectsFleetOrdersForFlagships(int ship)
    {
        StartFleet();
        Assert.Throws<ArgumentException>(() => Execute("fleet-follow", ship));
        Assert.All(Clients, client => Assert.Empty(Adapter(client).FleetOrders));
    }

    [Fact]
    public void ClientRejectsFleetOrderForAnotherOwnersHull()
    {
        StartFleet();
        var action = new NetworkNavalLabAction(Manifest.IncarnationId, Guid.NewGuid(), 1, "fleet-follow", 3, 0, false,
            DateTime.UtcNow.AddSeconds(2).Ticks);
        SendAction(First, action);
        Assert.Empty(Adapter(First).FleetOrders);
        Assert.Equal("rejected:fleet_owner_not_ready", Receipt(First, action.OperationId));
    }
}
#endif
