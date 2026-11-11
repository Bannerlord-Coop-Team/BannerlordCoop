using Missions.Battles;
using Moq;
using Newtonsoft.Json.Linq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

// #4213: a settled oar or helm seat sits at vanilla's spawn placement on the owner and every copy alike.
public class StationSeatSettlerTests
{
    private static readonly MatrixFrame StationFrame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 3f));
    private static readonly Vec3 SitDown = new Vec3(0f, -0.3f, -0.15f);
    private static readonly Vec3 SpawnSpot = new Vec3(10f, 19.7f, 2.85f);
    // An owner's rower that walked in sits about 17 cm above the spawn spot, as live B1 showed.
    private static readonly Vec3 WalkedIn = new Vec3(10f, 19.7f, 3.02f);

    [Fact]
    public void SpawnPlacement_PutsAnOarsRowerAtTheEndOfItsSitDownFromTheOrthonormalStationFrame()
    {
        var rotation = Mat3.Identity;
        rotation.RotateAboutUp((float)Math.PI / 2f);
        rotation.f *= 2f;
        var pose = Pose(StationSeatKind.Oar, new MatrixFrame(rotation, new Vec3(1f, 2f, 3f)), new Vec3(0.5f, 1f, -0.2f));

        var spot = StationSeatSettler.SpawnPlacement(pose);

        // Turned a quarter about up, forward is -x and side is +y; the doubled forward scale is dropped.
        AssertNear(new Vec3(0f, 2.5f, 2.8f), spot);
    }

    [Fact]
    public void SpawnPlacement_PutsTheHelmsPilotAtTheRawStationOrigin()
    {
        var pose = Pose(StationSeatKind.Helm, StationFrame, SitDown);

        AssertNear(StationFrame.origin, StationSeatSettler.SpawnPlacement(pose));
    }

    [Theory]
    [InlineData(StationSeatKind.Oar, true, false, true)]
    [InlineData(StationSeatKind.Helm, true, false, true)]
    [InlineData(StationSeatKind.Oar, false, false, false)]
    [InlineData(StationSeatKind.Helm, false, false, false)]
    [InlineData(StationSeatKind.Oar, true, true, false)]
    [InlineData(StationSeatKind.Helm, true, true, false)]
    [InlineData(StationSeatKind.None, true, false, false)]
    public void IsSettled_OnlyAPilotThatSatDownAndPlaysNoStagger(StationSeatKind kind, bool sitting, bool struck, bool settled)
    {
        var pose = new StationSeatPose(kind, sitting, struck, StationFrame, SitDown, WalkedIn);

        Assert.Equal(settled, StationSeatSettler.IsSettled(pose));
    }

    // The owner's rower walks in and sits down from deck height; once the sit-down ends it is placed at the spawn spot, once.
    [Fact]
    public void Settle_PlacesAWalkedInRowerOnceItsSitDownEnds()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, sitting: false, position: WalkedIn);
        var settler = new StationSeatSettler(seat.Engine.Object);

        seat.Settle(settler, times: 3);
        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Never);

        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        seat.Settle(settler, times: 3);

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.Is<Vec3>(spot => Near(spot, SpawnSpot))), Times.Once);
        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Once);
    }

    // A ram staggers a seated helm without a release; it is left alone while the stagger plays and placed back once it ends.
    [Fact]
    public void Settle_PlacesASeatAgainAfterAStaggerEnds_AndNotWhileItPlays()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Helm, StationFrame, SitDown, position: StationFrame.origin);
        var settler = new StationSeatSettler(seat.Engine.Object);
        seat.Settle(settler, times: 2);

        seat.Struck = true;
        seat.Pose = Pose(StationSeatKind.Helm, StationFrame, SitDown, struck: true, position: StationFrame.origin + new Vec3(0f, 0f, 0.055f));
        seat.Settle(settler, times: 4);
        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Never);

        seat.Struck = false;
        seat.Pose = Pose(StationSeatKind.Helm, StationFrame, SitDown, position: StationFrame.origin + new Vec3(0f, 0f, 0.055f));
        seat.Settle(settler, times: 3);

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.Is<Vec3>(spot => Near(spot, StationFrame.origin))), Times.Once);
        Assert.Equal(1, (long)JObject.FromObject(settler.Inspect())["alreadyPlaced"]!);
    }

    // A placed seat is read once more to see it stayed, then only watched for a stagger, without reading its machine every frame.
    [Fact]
    public void Settle_ReadsAPlacedSeatsPoseNoMoreOnceItStayed()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot);
        var settler = new StationSeatSettler(seat.Engine.Object);

        seat.Settle(settler, times: 5);

        seat.Engine.Verify(e => e.ReadSeatPose(seat.Agent, seat.Point), Times.Exactly(2));
        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Never);
    }

    [Theory]
    [InlineData("unseated")]
    [InlineData("hullSinking")]
    [InlineData("hullRemoved")]
    [InlineData("stationDetached")]
    [InlineData("stationOnAnotherHull")]
    [InlineData("notAPilotSeat")]
    [InlineData("noPoint")]
    public void Settle_NeverMovesAnAgentWhoseSeatIsNotHeldOnAValidStation(string guard)
    {
        var seat = new Seat();
        seat.Pose = Pose(guard == "notAPilotSeat" ? StationSeatKind.None : StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        if (guard == "unseated") seat.Engine.Setup(e => e.IsSeated(seat.Agent, seat.Point)).Returns(false);
        if (guard == "hullSinking") seat.Engine.Setup(e => e.IsSinking(seat.Hull)).Returns(true);
        if (guard == "hullRemoved") seat.Engine.Setup(e => e.IsHullRemoved(seat.Hull)).Returns(true);
        if (guard == "stationDetached") StationOf(seat.Engine, seat.Point, null, described: false);
        if (guard == "stationOnAnotherHull") StationOf(seat.Engine, seat.Point, ShipTestHulls.Create());
        var settler = new StationSeatSettler(seat.Engine.Object);

        for (int frame = 0; frame < 3; frame++)
        {
            settler.Settle(seat.AgentId, seat.Agent, guard == "noPoint" ? null! : seat.Point, seat.Hull);
            settler.EndPass();
        }

        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Never);
    }

    // A seat that is let go and taken again settles afresh, as does one moved to another point.
    [Fact]
    public void Settle_PlacesAgainAfterTheSeatIsLostOrMoved()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        var settler = new StationSeatSettler(seat.Engine.Object);
        seat.Settle(settler, times: 1);

        settler.EndPass();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        seat.Settle(settler, times: 1);
        seat.Engine.Setup(e => e.IsSeated(seat.Agent, seat.Point)).Returns(false);
        seat.Settle(settler, times: 1);
        seat.Engine.Setup(e => e.IsSeated(seat.Agent, seat.Point)).Returns(true);
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        seat.Settle(settler, times: 1);
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        var other = ShipTestHulls.CreatePoint();
        seat.Engine.Setup(e => e.IsSeated(seat.Agent, other)).Returns(true);
        seat.Engine.Setup(e => e.ReadSeatPose(seat.Agent, other)).Returns(() => seat.Pose);
        StationOf(seat.Engine, other, seat.Hull);
        settler.Settle(seat.AgentId, seat.Agent, other, seat.Hull);
        settler.EndPass();

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.IsAny<Vec3>()), Times.Exactly(4));
        Assert.Equal(1, (int)JObject.FromObject(settler.Inspect())["watched"]!);
    }

    // #4213 PvE: a copy seated by a received use sat at its spawn spot in the use's frame, the engine moved it 13 cm up on the
    // next frame as it had walked in, and it stayed there sitting in row idle. It is placed again once the next pass sees it moved.
    [Fact]
    public void Settle_PlacesAgainASeatTheEngineMovedOffItsSpotOnTheFrameAfterItWasSeated()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot);
        var settler = new StationSeatSettler(seat.Engine.Object);
        seat.Settle(settler, times: 1);

        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot + new Vec3(0f, 0.08f, 0.13f));
        seat.Settle(settler, times: 4);

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.Is<Vec3>(spot => Near(spot, SpawnSpot))), Times.Once);
        seat.Engine.Verify(e => e.PlaceSeatedAgent(It.IsAny<Agent>(), It.IsAny<Vec3>()), Times.Once);
        AssertNear(SpawnSpot, seat.Pose.Position);
        Assert.Equal(1, (long)JObject.FromObject(settler.Inspect())["slipped"]!);
    }

    // A seat the engine keeps moving off its spot is placed a bounded number of times, never once per frame for good.
    [Fact]
    public void Settle_StopsPlacingASeatThatNeverStaysAtItsSpot()
    {
        var seat = new Seat { PlacementHolds = false };
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: WalkedIn);
        var settler = new StationSeatSettler(seat.Engine.Object);

        seat.Settle(settler, times: 20);

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.IsAny<Vec3>()), Times.Exactly(StationSeatSettler.MaxPlacementsPerSettle));
        Assert.Equal(1, (long)JObject.FromObject(settler.Inspect())["gaveUp"]!);
    }

    // A release and a use of the same point between two passes leave the seat looking unchanged; the restart checks it again.
    [Fact]
    public void Restart_ChecksASeatTakenAgainOnTheSamePointBetweenPasses()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot);
        var settler = new StationSeatSettler(seat.Engine.Object);
        seat.Settle(settler, times: 2);

        settler.Restart(seat.AgentId);
        seat.Settle(settler, times: 1);
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot + new Vec3(0f, 0f, 0.13f));
        seat.Settle(settler, times: 2);

        seat.Engine.Verify(e => e.PlaceSeatedAgent(seat.Agent, It.Is<Vec3>(spot => Near(spot, SpawnSpot))), Times.Once);
    }

    // Seats no pass visits are forgotten, so the settler keeps no state for agents that left.
    [Fact]
    public void EndPass_ForgetsSeatsNoLongerVisited()
    {
        var seat = new Seat();
        seat.Pose = Pose(StationSeatKind.Oar, StationFrame, SitDown, position: SpawnSpot);
        var settler = new StationSeatSettler(seat.Engine.Object);
        seat.Settle(settler, times: 1);

        settler.EndPass();

        Assert.Equal(0, (int)JObject.FromObject(settler.Inspect())["watched"]!);
    }

    private static StationSeatPose Pose(StationSeatKind kind, MatrixFrame frame, Vec3 sitDown, bool sitting = true, bool struck = false,
        Vec3 position = default) =>
        new StationSeatPose(kind, sitting, struck, frame, sitDown, position);

    // Makes the engine describe the point as a station of the hull, or of none.
    private static void StationOf(Mock<INavalShipEngine> engine, UsableMissionObject point, MissionObject? hull, bool described = true)
    {
        var stationHull = hull!;
        string stationKey = "oar";
        int pointIndex = 0;
        engine.Setup(e => e.TryDescribeStation(point, out stationHull, out stationKey, out pointIndex)).Returns(described);
    }

    private static bool Near(Vec3 actual, Vec3 expected) => actual.DistanceSquared(expected) < 1e-8f;

    private static void AssertNear(Vec3 expected, Vec3 actual) =>
        Assert.True(Near(actual, expected), $"expected {expected}, got {actual}");

    // A held seat of an agent on a hull whose station resolves, with the pose and stagger the engine reports.
    private sealed class Seat
    {
        public Seat()
        {
            Engine.Setup(e => e.IsSeated(Agent, Point)).Returns(true);
            Engine.Setup(e => e.ReadSeatPose(Agent, Point)).Returns(() => Pose);
            Engine.Setup(e => e.IsStruck(Agent)).Returns(() => Struck);
            Engine.Setup(e => e.PlaceSeatedAgent(Agent, It.IsAny<Vec3>())).Callback<Agent, Vec3>((_, spot) =>
            {
                if (PlacementHolds) Pose = new StationSeatPose(Pose.Kind, Pose.IsSitting, Pose.IsStruck, Pose.StationFrame,
                    Pose.SitDownDisplacement, spot);
            });
            StationOf(Engine, Point, Hull);
        }

        public Guid AgentId { get; } = Guid.NewGuid();
        public Agent Agent { get; } = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        public UsableMissionObject Point { get; } = ShipTestHulls.CreatePoint();
        public MissionObject Hull { get; } = ShipTestHulls.Create();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public StationSeatPose Pose { get; set; }
        public bool Struck { get; set; }
        // Whether a placement moves the agent, or the engine moves it straight back off its spot.
        public bool PlacementHolds { get; set; } = true;

        // Runs the given number of mission ticks over this seat alone.
        public void Settle(StationSeatSettler settler, int times)
        {
            for (int frame = 0; frame < times; frame++)
            {
                settler.Settle(AgentId, Agent, Point, Hull);
                settler.EndPass();
            }
        }
    }
}
