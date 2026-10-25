using System;
using System.Linq;
using Common.Messaging;
using E2E.Tests.Environment.Extensions;
using E2E.Tests.Environment.Mock;
using E2E.Tests.Environment.MockEngine;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using LiteNetLib;
using Missions.Agents.Packets;
using Missions.Messages;
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
        public Guid HullId = Guid.NewGuid();
        public bool Resolves = true;
        public MatrixFrame Hull = new MatrixFrame(Mat3.Identity, new Vec3(10f, 20f, 0f));
        // Owner world directions are localised against the local hull at receive time.
        public MatrixFrame ReceiveHull;
        public NetPeer Sender = null!;
        private long sampleSequence;

        public void Receive(AgentData data, Guid[]? deckShips = null)
        {
            ReceiveHull = Hull;
            Handler.HandlePacket(Sender, new MovementPacket(new[] { Id }, new[] { data },
                sampleSequence: ++sampleSequence, deckShips: deckShips ?? new[] { HullId }));
        }

        public void Tick() => Handler.Interpolator.Tick(1f / 60f);

        public bool HasTarget => Handler.Interpolator.TryGetTargetFrame(Puppet, out _, out _, out _);

        public JToken Deck => JObject.FromObject(Handler.InspectShipDecks());

        public void MoveHull(float yaw, Vec3 translation)
        {
            Hull.rotation.RotateAboutUp(yaw);
            Hull.origin = Hull.origin + translation;
        }

        public AgentData Data(bool onDeck, Vec3 world)
        {
            Agent source = Mission.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(source, out var mirror));
            mirror.Position = world;
            mirror.MovementDirection = new Vec2(1f, 0f);
            mirror.LookDirection = new Vec3(1f, 0f, 0f);
            mirror.InputVector = new Vec2(0f, 1f);
            var data = new AgentData(source);
            if (onDeck) OnDeck(ref data, HullId, Local, 1.5f);
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
                Id = Guid.NewGuid(),
                Sender = NetPeerExtensions.CreatePeer()
            };
            peer.Resolve<IMessageBroker>().Publish(this, new NetworkMissionPeerEntered("owner", "movement-test"));
            peer.Resolve<IMissionContext>().MapPeer("owner", receiver.Sender);
            receiver.Puppet = receiver.Mission.SpawnAgent(
                new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(receiver.Puppet, out receiver.Mirror));
            receiver.Mirror.Position = receiver.Hull.TransformToParent(Local);
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("owner", receiver.Id, receiver.Puppet));
            if (configureDeck)
            {
                receiver.Handler.ConfigureShipDeck(new FakeShipDeckSeam
                {
                    Resolve = (agent, ship) =>
                        receiver.Resolves && ship == receiver.HullId && agent == receiver.Puppet ? receiver.Hull : null,
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
            receiver.Receive(receiver.Data(true, new Vec3(100f, 100f, 0f)));
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
    public void HullThatStopsResolving_EvictsAndHaltsTheDeckPuppet_AndWorldDeckTransitionsKeepTheTarget()
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            receiver.Receive(receiver.Data(true, Vec3.Zero));
            receiver.MoveHull(0.1f, new Vec3(0.3f, 0f, 0f));
            receiver.AssertFollowsHull();

            receiver.Resolves = false;
            receiver.Tick();
            Assert.False(receiver.HasTarget);
            Assert.Equal(Vec2.Zero, receiver.Mirror.InputVector);
            AssertNear(receiver.Mirror.Position.AsVec2, receiver.Mirror.LastTargetPosition);
            Assert.Equal(1, (long)receiver.Deck["interpolator"]!["deckEvictions"]!);

            receiver.Resolves = true;
            receiver.Receive(receiver.Data(true, Vec3.Zero));
            receiver.MoveHull(-0.1f, new Vec3(0f, 0.4f, 0f));
            receiver.AssertFollowsHull();

            Vec3 world = receiver.Mirror.Position + new Vec3(1f, 0f, 0f);
            receiver.Receive(receiver.Data(false, world));
            receiver.Tick();
            AssertNear(world.AsVec2, receiver.Mirror.LastTargetPosition);
            receiver.Receive(receiver.Data(true, Vec3.Zero));
            receiver.AssertFollowsHull();

            Assert.Equal(2, (long)receiver.Deck["interpolator"]!["deckTransitions"]!);
            Assert.Equal(0, receiver.Mirror.TeleportToPositionCalls);
        });
    }

    [Theory]
    [InlineData("no_resolver")]
    [InlineData("unresolved_hull")]
    [InlineData("index_outside_table")]
    [InlineData("non_finite_pose")]
    public void DeckPacket_WithoutResolvablePose_IsRejectedNotAppliedAsWorld(string condition)
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            if (condition == "no_resolver")
                receiver.Handler.ConfigureShipDeck(null);
            receiver.Resolves = condition != "unresolved_hull";
            AgentData data = receiver.Data(true, Vec3.Zero);
            if (condition == "non_finite_pose") OnDeck(ref data, receiver.HullId, new Vec3(float.NaN, 0f, 0f), 1f);

            receiver.Receive(data, condition == "index_outside_table" ? Array.Empty<Guid>() : null);
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
            AgentData data = receiver.Data(false, world);

            receiver.Receive(data);
            receiver.Tick();

            Assert.Equal(0, data.DeckShipIndex);
            Assert.Equal(world.AsVec2, receiver.Mirror.LastTargetPosition);
            Assert.Equal(0, receiver.Mirror.TeleportToPositionCalls);
        }, configureDeck: false);
    }

    [Fact]
    public void OwnedAgentOnAHull_IsStampedWithThePacketHullTable_AndSendsWorldWhenCaptureFails()
    {
        using var fixture = new MissionEngineFixture();
        var peer = Clients.First();
        SetControllerId(peer, "peer");
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            Agent sailor = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(sailor, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", Guid.NewGuid(), 1, sailor));
            var handler = peer.Resolve<ICoopMissionComponent>().AgentMovementHandler;
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            Guid hull = Guid.NewGuid();
            bool captures = true;
            handler.ConfigureShipDeck(new FakeShipDeckSeam
            {
                Capture = (info, world) => captures ? (hull, world - new Vec3(10f, 0f, 0f), 0.75f) : null,
            });

            mirror.Position = new Vec3(12f, 0f, 0f);
            mirror.RealGlobalVelocity = new Vec3(3f, 0f, 0f);
            handler.PollMovement(0f);
            MovementPacket deckPacket = Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());
            AgentData deck = Assert.Single(deckPacket.Agents);
            Assert.Equal(new[] { hull }, deckPacket.DeckShips);
            Assert.Equal(1, deck.DeckShipIndex);
            Assert.Equal(new Vec3(2f, 0f, 0f), deck.DeckLocal);
            Assert.Equal(0.75f, deck.Speed);

            captures = false;
            mirror.Position = new Vec3(13f, 0f, 0f);
            network.NetworkSentPackets.Packets.Clear();
            handler.PollMovement(0.1f);
            MovementPacket worldPacket = Assert.Single(network.NetworkSentPackets.GetPackets<MovementPacket>());
            AgentData world = Assert.Single(worldPacket.Agents);
            Assert.Null(worldPacket.DeckShips);
            Assert.Equal(0, world.DeckShipIndex);
            Assert.Equal(3f, world.Speed);
            Assert.Equal(1, (long)JObject.FromObject(handler.InspectShipDecks())["stamped"]!);
        });
    }

    // S13 D2: a deck pose that can't resolve drops the walking target and halts the puppet, as an evicted deck target does.
    [Theory]
    [InlineData("no_resolver")]
    [InlineData("unresolved_hull")]
    [InlineData("index_outside_table")]
    public void RejectedDeckPacket_HaltsAPuppetThatWasWalking(string condition)
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            Vec3 world = receiver.Mirror.Position + new Vec3(3f, 0f, 0f);
            receiver.Receive(receiver.Data(false, world));
            receiver.Tick();
            receiver.Mirror.InputVector = new Vec2(0f, 1f);

            if (condition == "no_resolver") receiver.Handler.ConfigureShipDeck(null);
            receiver.Resolves = condition != "unresolved_hull";
            receiver.Receive(receiver.Data(true, Vec3.Zero), condition == "index_outside_table" ? Array.Empty<Guid>() : null);

            Assert.False(receiver.HasTarget);
            Assert.Equal(Vec2.Zero, receiver.Mirror.InputVector);
            AssertNear(receiver.Mirror.Position.AsVec2, receiver.Mirror.LastTargetPosition);
        });
    }

    // S13 D3 (R43: one puppet teleported 2034 times): a deck puppet out of reach teleports once per owner update, then walks.
    // The puppet stays 7 m off its target, past the 6 m snap and under the DEBUG large-error log, which reads native state.
    [Fact]
    public void DeckPuppet_TeleportsAtMostOncePerOwnerUpdate()
    {
        using var fixture = new MissionEngineFixture();
        WithReceiver(fixture, receiver =>
        {
            receiver.Receive(receiver.Data(true, Vec3.Zero));
            receiver.MoveHull(0f, new Vec3(7f, 0f, 0f));
            receiver.Tick();
            receiver.MoveHull(0f, new Vec3(7f, 0f, 0f));
            receiver.Tick();
            receiver.Tick();
            int afterOneUpdate = receiver.Mirror.TeleportToPositionCalls;

            receiver.Receive(receiver.Data(true, Vec3.Zero));
            receiver.Tick();

            Assert.Equal(1, afterOneUpdate);
            Assert.Equal(2, receiver.Mirror.TeleportToPositionCalls);
            Assert.Equal(2, (long)receiver.Deck["interpolator"]!["deckTeleports"]!);
        });
    }

    // S13 A1: a deck agent's send decision compares its hull-local pose, so hull bob alone doesn't resend it every sample.
    [Fact]
    public void OwnedDeckAgent_StandingStillOnABobbingHull_SendsOnlyWhenItsDeckPoseMoves()
    {
        using var fixture = new MissionEngineFixture();
        WithOwnedSailor(fixture, (handler, mirror, network) =>
        {
            Vec3 local = new Vec3(2f, 0f, 0f);
            handler.ConfigureShipDeck(new FakeShipDeckSeam { Capture = (info, world) => (HullOf, local, 0f) });

            handler.PollMovement(0f);
            mirror.Position = mirror.Position + new Vec3(0f, 0f, 0.05f);
            handler.PollMovement(0.1f);
            int whileStill = network.NetworkSentPackets.GetPackets<MovementPacket>().Count();
            local = new Vec3(2.5f, 0f, 0f);
            handler.PollMovement(0.1f);

            Assert.Equal(1, whileStill);
            Assert.Equal(2, network.NetworkSentPackets.GetPackets<MovementPacket>().Count());
            Assert.Equal(3, (long)JObject.FromObject(handler.InspectShipDecks())["stamped"]!);
        });
    }

    // S13 D5: a seated agent sends no movement, and its first sample after leaving the station goes out at once.
    [Fact]
    public void OwnedSeatedAgent_SendsNoMovement_AndItsFirstSampleAfterLeavingGoesOutAtOnce()
    {
        using var fixture = new MissionEngineFixture();
        WithOwnedSailor(fixture, (handler, mirror, network) =>
        {
            bool seated = false;
            handler.ConfigureStationSeats(new FakeShipStationSeats { IsSeated = id => seated });

            handler.PollMovement(0f);
            seated = true;
            handler.PollMovement(0.1f);
            int whileSeated = network.NetworkSentPackets.GetPackets<MovementPacket>().Count();
            seated = false;
            handler.PollMovement(0.1f);

            Assert.Equal(1, whileSeated);
            Assert.Equal(2, network.NetworkSentPackets.GetPackets<MovementPacket>().Count());
            Assert.Equal(1, (long)JObject.FromObject(handler.InspectShipDecks())["seatedWithheld"]!);
        });
    }

    private static readonly Guid HullOf = Guid.NewGuid();

    // An owned on-foot sailor registered to this client, with its handler and the sent-packet log.
    private void WithOwnedSailor(MissionEngineFixture fixture, Action<IAgentMovementHandler, MirrorAgent, MockBattleNetwork> test)
    {
        var peer = Clients.First();
        SetControllerId(peer, "peer");
        peer.Call(() =>
        {
            var mock = CreateMovementMission(fixture, peer);
            Agent sailor = mock.SpawnAgent(new AgentBuildData(Game.Current.PlayerTroop).Controller(AgentControllerType.None));
            Assert.True(AgentMirror.TryGet(sailor, out var mirror));
            Assert.True(peer.Resolve<INetworkAgentRegistry>().TryRegisterAgent("peer", Guid.NewGuid(), 1, sailor));
            mirror.Position = new Vec3(12f, 0f, 0f);
            var network = Assert.IsType<MockBattleNetwork>(peer.Resolve<IBattleNetwork>());
            test(peer.Resolve<ICoopMissionComponent>().AgentMovementHandler, mirror, network);
        });
    }

    // Makes a received record a deck pose at the hull table's first entry.
    private static void OnDeck(ref AgentData data, Guid ship, Vec3 local, float speed)
    {
        data.CaptureDeck(ship, local, speed);
        data.IndexDeck(1);
    }

    private static void AssertNear(Vec2 expected, Vec2 actual)
    {
        Assert.InRange(actual.X, expected.X - 0.0005f, expected.X + 0.0005f);
        Assert.InRange(actual.Y, expected.Y - 0.0005f, expected.Y + 0.0005f);
    }
}
