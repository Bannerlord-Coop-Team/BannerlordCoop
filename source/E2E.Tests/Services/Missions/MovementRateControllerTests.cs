using Common.Messaging;
using Common.Serialization;
using GameInterface.Services.Entity;
using Missions;
using Missions.Agents.Handlers;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Threading;
using TaleWorlds.Engine;
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
    [InlineData(10)]
    [InlineData(600)]
    [InlineData(1800)]
    public void BattleProfile_PopulationDoesNotCapStartingRate(int activeAgents)
    {
        using var fixture = new RateControllerFixture(remoteControllers: 1);
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(activeAgents, activeAgents / 2);

        MovementRateSnapshot state = fixture.Controller.Snapshot;

        Assert.Equal(60, state.PerformanceCeilingHz);
        Assert.Equal(40, state.BulkHz);
        Assert.Equal(40, state.PriorityHz);
        Assert.Equal("battle-start", state.Reason);
    }

    [Fact]
    public void BattleProfile_SlowPeerOnlyCapsThatRecipient()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(10, 5);
        fixture.Broker.Publish(
            this,
            new NetworkMovementReceiverCap("slow-peer", 15, 1));

        MovementRateSnapshot capped = fixture.Controller.Snapshot;
        Assert.Equal(40, capped.BulkHz);
        Assert.Equal(40, capped.PriorityHz);
        Assert.Equal("slow-peer", capped.PeerReceiverCapSource);
        Assert.Equal(15, fixture.Controller.GetReceiverCapHz("slow-peer"));
        Assert.Equal(60, fixture.Controller.GetReceiverCapHz("healthy-peer"));

        fixture.Broker.Publish(
            this,
            new MissionPeerDisconnected("slow-peer", "battle"));

        MovementRateSnapshot restored = fixture.Controller.Snapshot;
        Assert.Equal(40, restored.BulkHz);
        Assert.Null(restored.PeerReceiverCapHz);
        Assert.Equal(60, fixture.Controller.GetReceiverCapHz("slow-peer"));
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
                Assert.Equal(40, advertisement.MaximumBulkHz);
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
        Assert.Equal(40, advertisement.MaximumBulkHz);
        Assert.Equal(2, advertisement.Sequence);
    }

    [Fact]
    public void BattleProfile_HeartbeatSendsAfterReleasingControllerGate()
    {
        MovementRateController controller = null;
        bool inspectHeartbeat = false;
        using var fixture = new RateControllerFixture(
            onSendAll: _ =>
            {
                if (!inspectHeartbeat) return;

                var advanceThread = new Thread(() => controller.AdvanceFrame(0f))
                {
                    IsBackground = true,
                };
                advanceThread.Start();
                Assert.True(advanceThread.Join(TimeSpan.FromSeconds(5)));
            });
        controller = fixture.Controller;
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        inspectHeartbeat = true;

        fixture.Controller.PublishReceiverCapHeartbeat();
    }

    [Fact]
    public void Dispose_WaitsForInFlightHeartbeat()
    {
        using var blockHeartbeat = new ManualResetEventSlim(false);
        using var heartbeatStarted = new ManualResetEventSlim(false);
        using var releaseHeartbeat = new ManualResetEventSlim(false);
        using var disposeStarted = new ManualResetEventSlim(false);

        using var fixture = new RateControllerFixture(
            enableHeartbeat: true,
            onSendAll: _ =>
            {
                if (!blockHeartbeat.IsSet) return;

                heartbeatStarted.Set();
                releaseHeartbeat.Wait();
            });
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        blockHeartbeat.Set();
        Assert.True(heartbeatStarted.Wait(TimeSpan.FromSeconds(5)));

        Exception disposeException = null;
        var disposeThread = new Thread(() =>
        {
            try
            {
                disposeStarted.Set();
                fixture.Controller.Dispose();
            }
            catch (Exception ex)
            {
                disposeException = ex;
            }
        });
        disposeThread.Start();

        try
        {
            Assert.True(disposeStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(disposeThread.Join(TimeSpan.FromMilliseconds(250)));
        }
        finally
        {
            releaseHeartbeat.Set();
            Assert.True(disposeThread.Join(TimeSpan.FromSeconds(5)));
        }

        Assert.Null(disposeException);
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
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(10, fixture.Controller.GetReceiverCapHz("slow-peer"));

        fixture.AdvanceClock(4f);

        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
        Assert.Null(fixture.Controller.Snapshot.PeerReceiverCapHz);
        Assert.Equal(60, fixture.Controller.GetReceiverCapHz("slow-peer"));
    }

    [Fact]
    public void BattleProfile_StartsFortyAndRecoversFromMeasuredPerformance()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        for (int i = 0; i < 3; i++)
            fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-recovered", fixture.Controller.Snapshot.Reason);

        fixture.AdvanceWindow(framesPerSecond: 60);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-recovered", fixture.Controller.Snapshot.Reason);
    }

    [Fact]
    public void BattleProfile_AttributesFortyToMeasuredPerformanceAfterFirstWindow()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 57);

        Assert.Equal(40, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-performance", fixture.Controller.Snapshot.Reason);
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
    public void BattleProfile_StableThirtyFpsRecoversAfterTransientQueueOverload()
    {
        using var fixture = new RateControllerFixture(frameLimitHz: 30);
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        MovementRateSnapshot initial = fixture.Controller.Snapshot;
        Assert.Equal(30, initial.FrameLimitHz);
        Assert.Equal(30, initial.PerformanceCeilingHz);
        Assert.Equal(30, initial.BulkHz);
        Assert.Equal(30, initial.AdvertisedReceiverCapHz);

        fixture.AdvanceWindow(
            framesPerSecond: 30,
            maximumReceiverQueueMilliseconds: 200d);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 3; i++)
            fixture.AdvanceWindow(framesPerSecond: 30);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 30);

        Assert.Equal(15, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(15, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
        Assert.Equal("battle-recovered", fixture.Controller.Snapshot.Reason);

        foreach (int expectedRate in new[] { 20, 30 })
        {
            for (int i = 0; i < 4; i++)
                fixture.AdvanceWindow(framesPerSecond: 30);

            Assert.Equal(expectedRate, fixture.Controller.Snapshot.BulkHz);
            Assert.Equal(expectedRate, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
        }

        for (int i = 0; i < 8; i++)
            fixture.AdvanceWindow(framesPerSecond: 30);

        Assert.Equal(30, fixture.Controller.Snapshot.FrameLimitHz);
        Assert.Equal(30, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(30, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
    }

    [Fact]
    public void BattleProfile_HighFpsSenderWorkAroundSixtyFiveMillisecondsRecoversToSixty()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(
            framesPerSecond: 60,
            maximumReceiverQueueMilliseconds: 200d);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        foreach (int expectedRate in new[] { 15, 20, 30, 40, 60 })
        {
            for (int i = 0; i < 4; i++)
            {
                fixture.AdvanceWindow(
                    framesPerSecond: 60,
                    senderMilliseconds: 65d);
            }

            Assert.Equal(expectedRate, fixture.Controller.Snapshot.BulkHz);
        }

        MovementRateSnapshot recovered = fixture.Controller.Snapshot;
        Assert.InRange(recovered.SenderMillisecondsPerSecond, 60d, 70d);
        Assert.Equal(60, recovered.BulkHz);
        Assert.Equal("battle-recovered", recovered.Reason);
    }

    [Fact]
    public void BattleProfile_RecoverySettlesAtMeasuredDutyCeiling()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(
            framesPerSecond: 60,
            maximumReceiverQueueMilliseconds: 200d);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        foreach (int expectedRate in new[] { 15, 20, 30, 40 })
        {
            for (int i = 0; i < 4; i++)
            {
                fixture.AdvanceWindow(
                    framesPerSecond: 60,
                    senderMilliseconds: 130d);
            }

            Assert.Equal(expectedRate, fixture.Controller.Snapshot.BulkHz);
        }

        for (int i = 0; i < 8; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 130d);
        }

        Assert.Equal(40, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-performance", fixture.Controller.Snapshot.Reason);
    }

    [Fact]
    public void BattleProfile_RejectedSenderTierWaitsForLowerPerPollCost()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 110d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 180d);
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 90d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 115d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 180d);
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 90d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 12; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 115d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 70d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-recovered", fixture.Controller.Snapshot.Reason);
    }

    [Fact]
    public void BattleProfile_RejectedFortyHzRetriesAfterBulkCostImproves()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 160d);
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 102d,
                prioritySenderMilliseconds: 15d);
        }

        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal("battle-recovered", fixture.Controller.Snapshot.Reason);
    }

    [Fact]
    public void BattleProfile_RejectedNonHarmonicTierRequiresAnotherCostImprovement()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 160d);
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 210d);
        Assert.Equal(20, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 140d,
                prioritySenderMilliseconds: 30d);
        }
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(
            framesPerSecond: 60,
            senderMilliseconds: 180d,
            prioritySenderMilliseconds: 30d);
        Assert.Equal(20, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 12; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 140d,
                prioritySenderMilliseconds: 30d);
        }
        Assert.Equal(20, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 120d,
                prioritySenderMilliseconds: 30d);
        }
        Assert.Equal(30, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(
            framesPerSecond: 60,
            senderMilliseconds: 180d,
            prioritySenderMilliseconds: 30d);
        Assert.Equal(20, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 12; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                senderMilliseconds: 120d,
                prioritySenderMilliseconds: 30d);
        }
        Assert.Equal(20, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_FrameRateRecoveryCanRetryWithoutLowerBulkCost()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 20, senderMilliseconds: 80d);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);

        foreach (int expectedRate in new[] { 15, 20, 30, 40 })
        {
            for (int i = 0; i < 4; i++)
            {
                fixture.AdvanceWindow(
                    framesPerSecond: 60,
                    senderMilliseconds: fixture.Controller.Snapshot.BulkHz * 2d);
            }

            Assert.Equal(expectedRate, fixture.Controller.Snapshot.BulkHz);
        }

        fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 80d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_FrameRateRecoveryRetriesAfterConfirmationFailure()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 70d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 57, senderMilliseconds: 70d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 70d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 57, senderMilliseconds: 70d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 57, senderMilliseconds: 70d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 70d);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);

        fixture.AdvanceWindow(framesPerSecond: 57, senderMilliseconds: 70d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 12; i++)
            fixture.AdvanceWindow(framesPerSecond: 60, senderMilliseconds: 70d);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_ReceiverApplyWorkKeepsConservativeCeiling()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(
            framesPerSecond: 60,
            receiverApplyMilliseconds: 200d);

        Assert.Equal(10, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(10, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
    }

    [Fact]
    public void BattleProfile_ReportsConfiguredFrameLimitAboveAdaptiveMaximum()
    {
        using var fixture = new RateControllerFixture(frameLimitHz: 360);
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        Assert.Equal(360, fixture.Controller.Snapshot.FrameLimitHz);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        for (int i = 0; i < 4; i++)
            fixture.AdvanceWindow(framesPerSecond: 60);

        Assert.Equal(360, fixture.Controller.Snapshot.FrameLimitHz);
        Assert.Equal(60, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_ActualFrameDropStillLowersRateBelowConfiguredLimit()
    {
        using var fixture = new RateControllerFixture(frameLimitHz: 60);
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        fixture.AdvanceWindow(framesPerSecond: 30);

        Assert.Equal(60, fixture.Controller.Snapshot.FrameLimitHz);
        Assert.Equal(15, fixture.Controller.Snapshot.PerformanceCeilingHz);
        Assert.Equal(15, fixture.Controller.Snapshot.BulkHz);
    }

    [Fact]
    public void BattleProfile_PersistentReceiverQueueOverloadRemainsAtMinimumRate()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);

        for (int i = 0; i < 12; i++)
        {
            fixture.AdvanceWindow(
                framesPerSecond: 60,
                maximumReceiverQueueMilliseconds: 200d);

            Assert.Equal(10, fixture.Controller.Snapshot.BulkHz);
            Assert.Equal(10, fixture.Controller.Snapshot.AdvertisedReceiverCapHz);
            Assert.Equal(10, fixture.Controller.Snapshot.PerformanceCeilingHz);
        }
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
    public void Metrics_CountActualPerRecipientPayloadBytes()
    {
        using var fixture = new RateControllerFixture(remoteControllers: 3);
        fixture.Controller.Configure(MovementCadenceProfile.Location);
        fixture.Controller.ReportSend(
            elapsedMilliseconds: 0d,
            new MovementTrafficFrame(125, 0, 0f),
            includesAuthoritativeAgents: true);

        fixture.Controller.AdvanceFrame(1f);

        Assert.Equal(125, fixture.Controller.Snapshot.WireBytesPerSecond);
    }

    [Fact]
    public void BattleProfile_ForcedRateSupportsBenchmarkSweep()
    {
        using var fixture = new RateControllerFixture();
        fixture.Controller.Configure(MovementCadenceProfile.Battle);
        fixture.Controller.ReportPopulation(1800, 900);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);

        Assert.True(fixture.Controller.TrySetForcedBulkHz(60, out string error));
        Assert.Null(error);
        Assert.Equal(60, fixture.Controller.Snapshot.BulkHz);
        Assert.Equal(60, fixture.Controller.Snapshot.PriorityHz);

        Assert.True(fixture.Controller.TrySetForcedBulkHz(null, out error));
        Assert.Null(error);
        Assert.Equal(40, fixture.Controller.Snapshot.BulkHz);
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

    [Fact]
    public void PublicConstructor_WithoutNativeConfigUsesAdaptiveMaximum()
    {
        Assert.Null(EngineApplicationInterface.IConfig);
        var network = new Mock<IBattleNetwork>();
        var controllerIdProvider = new Mock<IControllerIdProvider>();
        controllerIdProvider.SetupGet(provider => provider.ControllerId).Returns("local");
        var missionContext = new Mock<IMissionContext>();
        missionContext.SetupGet(context => context.ControllersInMission)
            .Returns(Array.Empty<string>());

        using var controller = new MovementRateController(
            network.Object,
            new MessageBroker(),
            controllerIdProvider.Object,
            missionContext.Object);

        Assert.Equal(60, controller.Snapshot.FrameLimitHz);
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

        public RateControllerFixture(
            int remoteControllers = 0,
            bool enableHeartbeat = false,
            int frameLimitHz = 60,
            Action<IMessage> onSendAll = null)
        {
            var network = new Mock<IBattleNetwork>();
            network
                .Setup(value => value.SendAll(It.IsAny<IMessage>()))
                .Callback<IMessage>(message =>
                {
                    if (message is NetworkMovementReceiverCap advertisement)
                        Advertisements.Add(advertisement);
                    onSendAll?.Invoke(message);
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
                () => frameLimitHz,
                enableHeartbeat);
        }

        public void AdvanceClock(float seconds)
        {
            timestamp += (long)(seconds * TimestampFrequency);
        }

        public void AdvanceWindow(
            int framesPerSecond,
            double senderMilliseconds = 0d,
            double prioritySenderMilliseconds = 0d,
            double receiverApplyMilliseconds = 0d,
            double maximumReceiverQueueMilliseconds = 0d)
        {
            if (senderMilliseconds > 0d)
            {
                int bulkReports = Controller.Snapshot.BulkHz;
                for (int i = 0; i < bulkReports; i++)
                {
                    Controller.ReportSend(
                        senderMilliseconds / bulkReports,
                        new MovementTrafficFrame(0, 0, 0f),
                        includesAuthoritativeAgents: true);
                }
            }
            if (prioritySenderMilliseconds > 0d)
            {
                Controller.ReportSend(
                    prioritySenderMilliseconds,
                    new MovementTrafficFrame(0, 0, 0f),
                    includesAuthoritativeAgents: false);
            }
            if (receiverApplyMilliseconds > 0d ||
                maximumReceiverQueueMilliseconds > 0d)
            {
                Controller.ReportReceive(
                    maximumReceiverQueueMilliseconds,
                    receiverApplyMilliseconds,
                    snapshots: 1);
            }

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
