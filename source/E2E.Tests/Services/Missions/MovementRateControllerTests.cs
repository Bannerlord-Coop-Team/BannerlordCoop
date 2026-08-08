using Common.Messaging;
using Common.Serialization;
using GameInterface.Services.Entity;
using Missions;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class MovementRateControllerTests
{
    [Fact]
    public void TournamentProfile_RemainsSixtyUnderLoadAndReceiverCaps()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Tournament);
        fixture.Controller.ReportPopulation(1800, 900);
        Assert.True(fixture.Controller.TrySetForcedReceiverCapHz(10, out _));
        fixture.Broker.Publish(
            this,
            new NetworkMovementReceiverCap("slow-peer", 10, 1));

        MovementRateSnapshot state = fixture.Controller.Snapshot;

        Assert.Equal(60, state.BulkHz);
        Assert.Equal(60, state.PriorityHz);
        Assert.Equal(10, state.AdvertisedReceiverCapHz);
        Assert.Equal(10, state.PeerReceiverCapHz);
        Assert.Equal("tournament-fixed", state.Reason);
    }

    [Fact]
    public void LocationProfile_RemainsForty()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Location);
        fixture.Controller.ReportPopulation(1800, 900);
        fixture.Broker.Publish(
            this,
            new NetworkMovementReceiverCap("slow-peer", 10, 1));

        MovementRateSnapshot state = fixture.Controller.Snapshot;

        Assert.Equal(40, state.BulkHz);
        Assert.Equal(40, state.PriorityHz);
        Assert.Equal("location-fixed", state.Reason);
    }

    [Theory]
    [InlineData(10, 60)]
    [InlineData(225, 40)]
    [InlineData(475, 30)]
    [InlineData(875, 20)]
    [InlineData(1375, 15)]
    [InlineData(1800, 10)]
    public void BattleProfile_AppliesPopulationCeiling(
        int activeAgents,
        int expectedHz)
    {
        using var fixture = new RateControllerFixture(remoteControllers: 1);
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(activeAgents, activeAgents / 2);

        MovementRateSnapshot state = fixture.Controller.Snapshot;

        Assert.Equal(expectedHz, state.LoadCeilingHz);
        Assert.Equal(expectedHz, state.BulkHz);
        Assert.Equal(Math.Max(40, expectedHz), state.PriorityHz);
    }

    [Fact]
    public void BattleProfile_SlowPeerCapsBulkUntilDisconnect()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(10, 5);
        fixture.Broker.Publish(
            this,
            new NetworkMovementReceiverCap("slow-peer", 15, 1));

        MovementRateSnapshot capped = fixture.Controller.Snapshot;
        Assert.Equal(15, capped.BulkHz);
        Assert.Equal(40, capped.PriorityHz);
        Assert.Equal("slow-peer", capped.PeerReceiverCapSource);

        fixture.Broker.Publish(
            this,
            new MissionPeerDisconnected("slow-peer", "battle"));

        MovementRateSnapshot restored = fixture.Controller.Snapshot;
        Assert.Equal(60, restored.BulkHz);
        Assert.Null(restored.PeerReceiverCapHz);
    }

    [Fact]
    public void BattleProfile_AdvertisesCapOnConfigureOverrideAndPeerEntry()
    {
        using var fixture = new RateControllerFixture();

        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        Assert.Collection(
            fixture.Advertisements,
            advertisement =>
            {
                Assert.Equal("local", advertisement.ControllerId);
                Assert.Equal(60, advertisement.MaximumBulkHz);
                Assert.Equal(1, advertisement.Sequence);
            });

        Assert.True(fixture.Controller.TrySetForcedReceiverCapHz(15, out _));
        fixture.Broker.Publish(
            this,
            new NetworkMissionPeerEntered("joining-peer", "battle"));

        Assert.Equal(2, fixture.Advertisements.Count);
        Assert.Equal(15, fixture.Advertisements[1].MaximumBulkHz);
        Assert.Equal(2, fixture.Advertisements[1].Sequence);

        var directed = Assert.Single(fixture.DirectedAdvertisements);
        Assert.Equal("joining-peer", directed.RecipientControllerId);
        Assert.Equal(15, directed.Advertisement.MaximumBulkHz);
        Assert.Equal(3, directed.Advertisement.Sequence);
    }

    [Fact]
    public void BattleProfile_HeartbeatsWithoutAdvancingFrames()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Advertisements.Clear();

        fixture.Controller.PublishReceiverCapHeartbeat();

        NetworkMovementReceiverCap advertisement = Assert.Single(fixture.Advertisements);
        Assert.Equal("local", advertisement.ControllerId);
        Assert.Equal(60, advertisement.MaximumBulkHz);
        Assert.Equal(2, advertisement.Sequence);
    }

    [Fact]
    public void BattleProfile_ExpiresStalePeerCap()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(10, 5);
        fixture.Broker.Publish(
            this,
            new NetworkMovementReceiverCap("slow-peer", 10, 1));
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceClock(4f);

        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Null(fixture.Controller.Snapshot.PeerReceiverCapHz);
    }

    [Fact]
    public void BattleProfile_PerformanceRecoveryUsesHysteresis()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(10, 5);

        fixture.AdvanceWindow(framesPerSecond: 20);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 3; i++)
            fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(15, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_ReceiverCapRecoveryUsesStepwiseHysteresis()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 20);
        Assert.Equal(10, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);

        for (int i = 0; i < 3; i++)
            fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(10, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);

        fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(15, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
    }

    [Fact]
    public void Metrics_MultiplySerializedPayloadByPeerFanout()
    {
        using var fixture = new RateControllerFixture(remoteControllers: 3);
        fixture.Controller.Configure(MovementCadenceProfile.Location);
        fixture.Controller.ReportSend(
            elapsedMilliseconds: 0d,
            new MovementTrafficFrame(125, 0, 0f),
            includesAuthoritativeAgents: true);

        fixture.Controller.AdvanceFrame(1f);

        Assert.Equal(375, fixture.Controller.Snapshot.WireBytesPerSecond);
    }

    [Fact]
    public void BattleProfile_ForcedRateSupportsBenchmarkSweep()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(1800, 900);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        Assert.True(fixture.Controller.TrySetForcedBulkHz(60, out string error));
        Assert.Null(error);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(60, fixture.Controller.Snapshot.PriorityHz);

        Assert.True(fixture.Controller.TrySetForcedBulkHz(null, out error));
        Assert.Null(error);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(40, fixture.Controller.Snapshot.PriorityHz);
    }

    [Fact]
    public void ReceiverCapMessage_RoundTrips()
    {
        var serializer = new ProtoBufSerializer(new SerializableTypeMapper());
        var expected = new NetworkMovementReceiverCap("player-two", 15, 42);

        byte[] payload = serializer.Serialize(expected);
        var actual = serializer.Deserialize<NetworkMovementReceiverCap>(payload);

        Assert.Equal(expected.ControllerId, actual.ControllerId);
        Assert.Equal(expected.MaximumBulkHz, actual.MaximumBulkHz);
        Assert.Equal(expected.Sequence, actual.Sequence);
    }

    private sealed class RateControllerFixture : IDisposable
    {
        private long timestamp;
        private const long TimestampFrequency = 1000;

        public MessageBroker Broker { get; } = new MessageBroker();
        public System.Collections.Generic.List<NetworkMovementReceiverCap> Advertisements { get; } =
            new System.Collections.Generic.List<NetworkMovementReceiverCap>();
        public System.Collections.Generic.List<(string RecipientControllerId, NetworkMovementReceiverCap Advertisement)>
            DirectedAdvertisements { get; } =
                new System.Collections.Generic.List<(string, NetworkMovementReceiverCap)>();
        public MovementRateController Controller { get; }

        public RateControllerFixture(int remoteControllers = 0)
        {
            var network = new Mock<IBattleNetwork>();
            network
                .Setup(value => value.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message =>
                {
                    if (message is NetworkMovementReceiverCap advertisement)
                        Advertisements.Add(advertisement);
                });
            network
                .Setup(value => value.Send(It.IsAny<string>(), It.IsAny<IMessage>()))
                .Callback<string, IMessage>((recipientControllerId, message) =>
                {
                    if (message is NetworkMovementReceiverCap advertisement)
                        DirectedAdvertisements.Add((recipientControllerId, advertisement));
                });
            var controllerIdProvider = new Mock<IControllerIdProvider>();
            controllerIdProvider.SetupGet(provider => provider.ControllerId)
                .Returns("local");
            var missionContext = new Mock<IMissionContext>();
            var controllers = new string[remoteControllers];
            for (int i = 0; i < controllers.Length; i++)
                controllers[i] = $"remote-{i}";
            missionContext.SetupGet(context => context.ControllersInMission)
                .Returns(controllers);

            Controller = new MovementRateController(
                network.Object,
                Broker,
                controllerIdProvider.Object,
                missionContext.Object,
                () => timestamp,
                TimestampFrequency,
                enableHeartbeat: false);
        }

        public void AdvanceClock(float seconds)
        {
            timestamp += (long)(seconds * TimestampFrequency);
        }

        public void AdvanceWindow(int framesPerSecond)
        {
            float dt = 1f / framesPerSecond;
            for (int i = 0; i <= framesPerSecond; i++)
            {
                Controller.AdvanceFrame(dt);
                AdvanceClock(dt);
            }
        }

        public void Dispose()
        {
            Controller.Dispose();
            Broker.Dispose();
        }
    }
}
