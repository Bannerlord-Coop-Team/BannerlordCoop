#if DEBUG
using System;
using System.Linq;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Missions.Services.Network;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;
using AgentData = Missions.Agents.Packets.AgentData;

namespace E2E.Tests.Services.Missions;

public sealed class NavalDeckMovementTests : MissionTestEnvironment
{
    private static readonly Vec3 Local = new Vec3(1f, 2f, 0.5f);

    public NavalDeckMovementTests(ITestOutputHelper output) : base(output) { }

    private sealed class Receiver
    {
        public IAgentMovementHandler Handler = null!;
        public MockMission Mission = null!;
        public Agent Puppet = null!;
        public MirrorAgent Mirror = null!;
        public Guid Id;
        public long Expected = 2;
        public bool Resolves = true;
        public MatrixFrame Hull = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 0f));
        // Owner world directions are localised against the follower hull at receive time.
        public MatrixFrame ReceiveHull;

        public void Receive(AgentData data)
        {
            ReceiveHull = Hull;
            Handler.HandlePacket(null, new MovementPacket(new[] { Id }, new[] { data }));
        }

        public void Tick() => Handler.Interpolator.Tick(1f / 60f);

        public bool HasTarget => Handler.Interpolator.TryGetTargetFrame(Puppet, out _, out _, out _);

        public JToken Deck => JObject.FromObject(Handler.InspectNavalStationMovement())["deck"]!;

        public void MoveHull(float yaw, Vec3 translation)
        {
            Hull.rotation.RotateAboutUp(yaw);
            Hull.origin = Hull.origin + translation;
        }

        public AgentData Data(long revision, int deckShip, Vec3 world)
        {
            Agent source = Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(source, out var mirror));
            mirror.Position = world;
            mirror.MovementDirection = new Vec2(1f, 0f);
            mirror.LookDirection = new Vec3(1f, 0f, 0f);
            mirror.InputVector = new Vec2(0f, 1f);
            var data = new AgentData(source);
            data.NavalHelmRevision = revision;
            if (deckShip != 0) data.StampNavalDeck(deckShip, Local, 1.5f);
            return data;
        }

        public void AssertFollowsHull()
        {
            Tick();
            AssertNear(Hull.TransformToParent(Local).AsVec2, Mirror.LastTargetPosition);
            Vec3 localForward = ReceiveHull.rotation.TransformToLocal(new Vec3(1f, 0f, 0f));
            Vec2 forward = Hull.rotation.TransformToParent(localForward).AsVec2;
            forward.Normalize();
            AssertNear(forward, Mirror.MovementDirection);
            AssertNear(forward, Mirror.LookDirection.AsVec2);
        }
    }

    private void WithReceiver(MissionEngineFixture fixture, Action<Receiver> test, bool configureDeck = true)
    {
        var peer = Clients.First();
        SetControllerId(peer, "peer");
        peer.Call(() =>
        {
            var receiver = new Receiver
            {
                Mission = CreateMovementMission(fixture, peer),
                Handler = peer.Resolve<ICoopMissionComponent>().AgentMovementHandler,
                Id = Guid.NewGuid()
            };
            receiver.Puppet = receiver.Mission.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(receiver.Puppet, out receiver.Mirror));
            receiver.Mirror.Position = receiver.Hull.TransformToParent(Local);
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", receiver.Id, receiver.Puppet));
            if (configureDeck)
            {
                receiver.Handler.ConfigureNavalStationMovement(_ => false, null, null,
                    (_, revision) => revision == receiver.Expected, null,
                    (Agent agent, int ship, out MatrixFrame frame) =>
                    {
                        frame = receiver.Hull;
                        return receiver.Resolves && ship == 1 && agent == receiver.Puppet;
                    });
            }
            test(receiver);
        });
    }

    [Fact]
    public void DeckTarget_FollowsTranslatedAndRotatedHullEveryTick_WithoutNewPacket()
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            receiver.Receive(receiver.Data(2, 1, new Vec3(100f, 100f, 0f)));
            receiver.AssertFollowsHull();
            for (int i = 0; i < 3; i++)
            {
                receiver.MoveHull(0.2f, new Vec3(0.5f, 0.2f, 0f));
                receiver.AssertFollowsHull();
            }

            Assert.Equal(0, receiver.Mirror.TeleportToPositionCalls);
            Assert.Equal(4, (long)receiver.Deck["interpolator"]!["deckTicks"]!);
            Assert.Equal(1, (long)receiver.Deck["accepted"]!);
        });
    }

    [Fact]
    public void ReleaseReseatAndModeTransitions_RejectStaleRevisionsWithoutTeleport()
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            AgentData released = receiver.Data(2, 1, Vec3.Zero);
            receiver.Receive(released);
            receiver.MoveHull(0.1f, new Vec3(0.3f, 0f, 0f));
            receiver.AssertFollowsHull();

            // Re-seat: the replica is no longer released, so the next tick evicts and halts the deck puppet.
            receiver.Resolves = false;
            receiver.Tick();
            Assert.False(receiver.HasTarget);
            Assert.Equal(Vec2.Zero, receiver.Mirror.InputVector);
            AssertNear(receiver.Mirror.Position.AsVec2, receiver.Mirror.LastTargetPosition);
            Assert.Equal(1, (long)receiver.Deck["interpolator"]!["deckEvictions"]!);

            receiver.Expected = -1;
            receiver.Receive(released);
            Assert.False(receiver.HasTarget);

            // Next release: the late previous revision is rejected and only the current one is followed.
            receiver.Expected = 4;
            receiver.Resolves = true;
            receiver.Receive(released);
            Assert.False(receiver.HasTarget);
            receiver.Receive(receiver.Data(4, 1, Vec3.Zero));
            receiver.MoveHull(-0.1f, new Vec3(0f, 0.4f, 0f));
            receiver.AssertFollowsHull();

            Vec3 world = receiver.Mirror.Position + new Vec3(1f, 0f, 0f);
            receiver.Receive(receiver.Data(4, 0, world));
            receiver.Tick();
            AssertNear(world.AsVec2, receiver.Mirror.LastTargetPosition);
            receiver.Receive(receiver.Data(4, 1, Vec3.Zero));
            receiver.AssertFollowsHull();

            Assert.Equal(2, (long)receiver.Deck["interpolator"]!["deckTransitions"]!);
            Assert.Equal(0, receiver.Mirror.TeleportToPositionCalls);
        });
    }

    [Theory]
    [InlineData("no_resolver")]
    [InlineData("unresolved_hull")]
    [InlineData("unreleased_revision")]
    [InlineData("non_finite_pose")]
    public void DeckPacket_WithoutResolvableReleasedPose_IsRejectedNotAppliedAsWorld(string condition)
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            if (condition == "no_resolver")
                receiver.Handler.ConfigureNavalStationMovement(_ => false, null, null, (_, _) => true);
            receiver.Resolves = condition != "unresolved_hull";
            receiver.Expected = condition == "unreleased_revision" ? 0 : 2;
            AgentData data = receiver.Data(receiver.Expected, 1, Vec3.Zero);
            if (condition == "non_finite_pose") data.StampNavalDeck(1, new Vec3(float.NaN, 0f, 0f), 1f);

            receiver.Receive(data);
            receiver.Tick();

            Assert.False(receiver.HasTarget);
            Assert.Equal(0, receiver.Mirror.SetTargetPositionAndDirectionCalls + receiver.Mirror.TeleportToPositionCalls);
            Assert.Equal(1, (long)receiver.Deck["rejected"]!);
        });
    }

    [Fact]
    public void OrdinaryWorldPacket_KeepsWorldTargetWithoutNavalConfiguration()
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            Vec3 world = receiver.Mirror.Position + new Vec3(2f, -1f, 0f);
            AgentData data = receiver.Data(0, 0, world);

            receiver.Receive(data);
            receiver.Tick();

            Assert.Equal(0, data.NavalDeckShip);
            Assert.Equal(world.AsVec2, receiver.Mirror.LastTargetPosition);
            Assert.Equal(0, receiver.Mirror.TeleportToPositionCalls);
        }, configureDeck: false);
    }

    [Fact]
    public void ReleasedCaptainPacket_IsStampedOnDeck_AndCountsWorldFallbackWhenCaptureFails()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            Agent captain = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(captain, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", Guid.NewGuid(), 1, captain));
            var handler = peer.Resolve<ICoopMissionComponent>().AgentMovementHandler;
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            bool captures = true;
            handler.ConfigureNavalStationMovement(_ => false, _ => false, _ => 2, null,
                (CoopAgentInfo info, Vec3 world, out int ship, out Vec3 local, out float speed) =>
                {
                    ship = 1;
                    local = world - new Vec3(10f, 0f, 0f);
                    speed = 0.75f;
                    return captures;
                });

            mirror.Position = new Vec3(12f, 0f, 0f);
            mirror.RealGlobalVelocity = new Vec3(3f, 0f, 0f);
            handler.PollMovement(0f);
            AgentData deck = Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>().SelectMany(p => p.Agents));
            Assert.Equal(2, deck.NavalHelmRevision);
            Assert.Equal(1, deck.NavalDeckShip);
            Assert.Equal(new Vec3(2f, 0f, 0f), deck.NavalDeckLocal);
            Assert.Equal(0.75f, deck.Speed);

            captures = false;
            mirror.Position = new Vec3(13f, 0f, 0f);
            network.NetworkSentPackets.Packets.Clear();
            handler.PollMovement(0.1f);
            AgentData world = Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>().SelectMany(p => p.Agents));
            Assert.Equal(2, world.NavalHelmRevision);
            Assert.Equal(0, world.NavalDeckShip);
            Assert.Equal(3f, world.Speed);

            var status = JObject.FromObject(handler.InspectNavalStationMovement())["deck"]!;
            Assert.Equal(1, (long)status["stamped"]!);
            Assert.Equal(1, (long)status["worldFallback"]!);
        });
    }

    private static void AssertNear(Vec2 expected, Vec2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0005f, expected.X + 0.0005f);
        Assert.InRange(actual.Y, expected.Y - 0.0005f, expected.Y + 0.0005f);
    }
}
#endif
