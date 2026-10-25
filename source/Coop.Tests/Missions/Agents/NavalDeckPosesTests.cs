using Coop.Tests.Missions.Battles;
using Missions;
using Missions.Agents.Handlers;
using Missions.Battles;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class NavalDeckPosesTests
{
    private static readonly Vec2 Min = new Vec2(-4f, -15f);
    private static readonly Vec2 Max = new Vec2(4f, 15f);
    private static readonly MatrixFrame HullFrame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 0f));

    // A deck agent, or one on a plank reaching past the side, keeps a hull-local pose.
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(4f + NavalDeckPoses.FootprintMarginMetres, 15f)]
    [InlineData(-14f, -20f)]
    public void IsOverFootprint_AcceptsAPoseOnTheHullOrWithinTheMargin(float x, float y)
    {
        Assert.True(NavalDeckPoses.IsOverFootprint(new Vec3(x, y, -4.6f), Min, Max));
    }

    // R43: corsair 4f4d9701 sent deckLocal (-334, 328, -4.6) on 1efe6d0f, about 470 m off that hull.
    [Theory]
    [InlineData(-334.3f, 328.4f)]
    [InlineData(4.1f + NavalDeckPoses.FootprintMarginMetres, 0f)]
    [InlineData(0f, -15.1f - NavalDeckPoses.FootprintMarginMetres)]
    public void IsOverFootprint_RefusesAPoseFarOffTheHull(float x, float y)
    {
        Assert.False(NavalDeckPoses.IsOverFootprint(new Vec3(x, y, -4.6f), Min, Max));
    }

    // S13 D4: the support hull resolves to its registry id and the pose goes hull-local in the hull's frame.
    [Fact]
    public void TryCaptureDeck_ReturnsTheRegisteredSupportHullAndTheHullLocalPose()
    {
        var deck = new Deck();
        using var poses = deck.Create();

        bool captured = poses.TryCaptureDeck(deck.Info, new Vec3(12f, 23f, 1f), out Guid ship, out Vec3 local, out float speed);

        Assert.True(captured);
        Assert.Equal(deck.ShipId, ship);
        Assert.Equal(new Vec3(2f, 3f, 1f), local);
        Assert.Equal(0f, speed);
    }

    [Fact]
    public void TryCaptureDeck_SendsWorldForAnUnregisteredHullOrAPoseOffItsFootprint()
    {
        var deck = new Deck();
        using var poses = deck.Create();
        deck.Engine.Setup(e => e.GetSupportHull(It.IsAny<Agent>())).Returns(ShipTestHulls.Create());
        bool unregistered = poses.TryCaptureDeck(deck.Info, new Vec3(12f, 23f, 1f), out _, out _, out _);

        deck.Engine.Setup(e => e.GetSupportHull(It.IsAny<Agent>())).Returns(deck.Hull);
        Vec2 min = Min, max = Max;
        deck.Engine.Setup(e => e.TryGetFootprint(deck.Hull, out min, out max)).Returns(true);
        bool offFootprint = poses.TryCaptureDeck(deck.Info, new Vec3(400f, 20f, 0f), out _, out Vec3 local, out _);

        Assert.False(unregistered);
        Assert.False(offFootprint);
        Assert.Equal(Vec3.Zero, local);
    }

    // Deck speed is a finite difference of hull-local poses: reset on a new ship or a gap over 0.5 s, held inside 5 ms.
    [Fact]
    public void TryCaptureDeck_MeasuresDeckSpeedFromHullLocalPoses()
    {
        var deck = new Deck();
        using var poses = deck.Create();

        float first = Speed(poses, deck, 12f);
        poses.Tick(0.1f);
        float walking = Speed(poses, deck, 12.5f);
        float repeated = Speed(poses, deck, 13f);
        poses.Tick(0.6f);
        float afterGap = Speed(poses, deck, 14f);

        Assert.Equal(0f, first);
        Assert.Equal(5f, walking, 3);
        Assert.Equal(walking, repeated);
        Assert.Equal(0f, afterGap);
    }

    [Fact]
    public void TryResolveDeckFrame_ResolvesOnlyARegisteredHull()
    {
        var deck = new Deck();
        using var poses = deck.Create();

        bool registered = poses.TryResolveDeckFrame(deck.Info.Agent, deck.ShipId, out MatrixFrame frame);
        bool unknown = poses.TryResolveDeckFrame(deck.Info.Agent, Guid.NewGuid(), out _);

        Assert.True(registered);
        Assert.Equal(HullFrame.origin, frame.origin);
        Assert.False(unknown);
    }

    private static float Speed(NavalDeckPoses poses, Deck deck, float worldX)
    {
        Assert.True(poses.TryCaptureDeck(deck.Info, new Vec3(worldX, 20f, 0f), out _, out _, out float speed));
        return speed;
    }

    // A registered hull with a valid frame that the engine names as the support of an owned agent.
    private sealed class Deck
    {
        public Deck()
        {
            var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            Info = new CoopAgentInfo("own", "own", "battle", agent, Guid.NewGuid(), 1);
            Ships.TryRegister(new NetworkShipInfo(ShipId, "own", "party", isNpcParty: false, Hull, null!));
            Engine.Setup(e => e.GetSupportHull(It.IsAny<Agent>())).Returns(Hull);
            Engine.Setup(e => e.GetFrame(Hull)).Returns(HullFrame);
        }

        public Guid ShipId { get; } = Guid.NewGuid();
        public MissionObject Hull { get; } = ShipTestHulls.Create();
        public CoopAgentInfo Info { get; }
        public NetworkShipRegistry Ships { get; } = new NetworkShipRegistry();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();

        public NavalDeckPoses Create()
        {
            var component = new Mock<ICoopMissionComponent>();
            component.SetupGet(c => c.AgentMovementHandler).Returns(Mock.Of<IAgentMovementHandler>());
            component.SetupGet(c => c.AgentActionHandler).Returns(Mock.Of<IAgentActionHandler>());
            component.SetupGet(c => c.ShipRegistry).Returns(Ships);
            return new NavalDeckPoses(component.Object, Engine.Object, Mock.Of<IAgentStationUseReplicator>());
        }
    }
}
