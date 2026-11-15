using Common.Network.Session;
using Missions;
using Missions.Agents.Handlers;
using Moq;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class MovementRouteBackoffTests
{
    private const string Route = "steam-peer";
    private const string OtherRoute = "healthy-peer";
    private const double FullBattleBytesPerSecond = 954_000d;
    private const int BaselineRoundTrip = 190;
    private const int SaturatedRoundTrip = 1900;

    private readonly Dictionary<string, RouteHealth> healthByRoute = new();
    private readonly MovementRouteBackoff backoff;

    private delegate bool TryGetRouteHealthCallback(string controllerId, out RouteHealth health);

    public MovementRouteBackoffTests()
    {
        var network = new Mock<IBattleNetwork>();
        network
            .Setup(value => value.TryGetRouteHealth(It.IsAny<string>(), out It.Ref<RouteHealth>.IsAny))
            .Returns(new TryGetRouteHealthCallback((string controllerId, out RouteHealth health) =>
                healthByRoute.TryGetValue(controllerId, out health)));
        backoff = new MovementRouteBackoff(network.Object);
    }

    // The failing ticket: receiver queue and deferred counts looked fine, only the route's ping moved.
    [Fact]
    public void RisingRoundTrip_BacksOffFromTheRateActuallySent()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, BaselineRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip);
        Assert.Equal(double.PositiveInfinity, backoff.GetBytesPerSecond(Route));

        SampleRoundTrips(FullBattleBytesPerSecond, SaturatedRoundTrip);

        Assert.Equal(FullBattleBytesPerSecond * 0.7d, backoff.GetBytesPerSecond(Route), precision: 3);
    }

    // LiteNetLib folds a lost pong into its average as a sample the length of the ping interval.
    [Fact]
    public void AveragedRoundTripFromOneLostPong_DoesNotBackOff()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, 60, 530, 373, 295, 177, 138, 100, 60);

        Assert.Equal(double.PositiveInfinity, backoff.GetBytesPerSecond(Route));
    }

    [Fact]
    public void RoundTripStillFallingAfterACut_HoldsUntilItStopsFalling()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, BaselineRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip);
        double firstCut = backoff.GetBytesPerSecond(Route);

        SampleRoundTrips(firstCut, 1500, 1500);
        Assert.Equal(firstCut, backoff.GetBytesPerSecond(Route));

        SampleRoundTrips(firstCut, 1500, 1500);
        Assert.Equal(firstCut * 0.7d, backoff.GetBytesPerSecond(Route), precision: 3);
    }

    [Fact]
    public void LastingCongestion_StopsAtTheFloor()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, BaselineRoundTrip);
        for (int i = 0; i < 16; i++)
            SampleRoundTrips(FullBattleBytesPerSecond, SaturatedRoundTrip);

        Assert.Equal(MovementRouteBackoff.FloorBytesPerSecond, backoff.GetBytesPerSecond(Route));
    }

    [Fact]
    public void CongestedRoute_DoesNotLowerAnotherRoutesCeiling()
    {
        healthByRoute[OtherRoute] = new RouteHealth(BaselineRoundTrip, 1f, 0);
        foreach (int roundTrip in new[] { BaselineRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip })
        {
            healthByRoute[Route] = new RouteHealth(roundTrip, RouteHealth.UnknownQuality, 0);
            backoff.ReportSent(Route, (int)FullBattleBytesPerSecond);
            backoff.ReportSent(OtherRoute, (int)FullBattleBytesPerSecond);
            backoff.Advance(1f);
        }

        Assert.True(backoff.GetBytesPerSecond(Route) < FullBattleBytesPerSecond);
        Assert.Equal(double.PositiveInfinity, backoff.GetBytesPerSecond(OtherRoute));
    }

    [Theory]
    [InlineData(0.5f, 0L)]
    [InlineData(RouteHealth.UnknownQuality, 2L * 1024 * 1024)]
    public void DeliveryOrBacklogProblemWithFlatRoundTrip_BacksOff(float quality, long queuedBytes)
    {
        for (int i = 0; i < 3; i++)
        {
            healthByRoute[Route] = new RouteHealth(BaselineRoundTrip, quality, queuedBytes);
            backoff.ReportSent(Route, (int)FullBattleBytesPerSecond);
            backoff.Advance(1f);
        }

        Assert.True(backoff.GetBytesPerSecond(Route) < FullBattleBytesPerSecond);
    }

    [Fact]
    public void HealthyRoute_GrowsAfterTheCooldownOnlyWhileMovementUsesTheCeiling()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, BaselineRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip);
        double ceiling = backoff.GetBytesPerSecond(Route);

        for (int i = 0; i < 5; i++)
            SampleRoundTrips(ceiling, BaselineRoundTrip);
        Assert.Equal(ceiling, backoff.GetBytesPerSecond(Route));

        SampleRoundTrips(ceiling * 0.5d, BaselineRoundTrip);
        Assert.Equal(ceiling, backoff.GetBytesPerSecond(Route));

        SampleRoundTrips(ceiling, BaselineRoundTrip);
        Assert.Equal(
            ceiling + MovementRouteBackoff.GrowthBytesPerSecondPerSample,
            backoff.GetBytesPerSecond(Route));
    }

    // A transport timeout moves sends to the relay without a membership change, so nothing removes the route.
    [Fact]
    public void RouteFallingBackToTheRelay_RampsUpAndAReturningPeerStartsAFreshBaseline()
    {
        SampleRoundTrips(FullBattleBytesPerSecond, BaselineRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip, SaturatedRoundTrip);
        double ceiling = backoff.GetBytesPerSecond(Route);

        healthByRoute.Remove(Route);
        for (int i = 0; i < 6; i++)
        {
            backoff.ReportSent(Route, (int)ceiling);
            backoff.Advance(1f);
        }
        double recovered = ceiling + MovementRouteBackoff.GrowthBytesPerSecondPerSample;
        Assert.Equal(recovered, backoff.GetBytesPerSecond(Route));

        SampleRoundTrips(recovered, BaselineRoundTrip + 400, BaselineRoundTrip + 400, BaselineRoundTrip + 400);
        Assert.True(backoff.GetBytesPerSecond(Route) >= recovered);
    }

    // A route switch or another app raises RTT by itself; movement this small cannot be the cause.
    [Fact]
    public void LastingRoundTripWithMinimalMovement_BecomesTheNewBaseline()
    {
        const double deploymentBytesPerSecond = 50_000d;
        SampleRoundTrips(deploymentBytesPerSecond, BaselineRoundTrip);
        for (int i = 0; i < 12; i++)
            SampleRoundTrips(deploymentBytesPerSecond, SaturatedRoundTrip);
        Assert.Equal(double.PositiveInfinity, backoff.GetBytesPerSecond(Route));

        for (int i = 0; i < 5; i++)
            SampleRoundTrips(FullBattleBytesPerSecond, SaturatedRoundTrip);

        Assert.Equal(double.PositiveInfinity, backoff.GetBytesPerSecond(Route));
    }

    /// <summary>Drives one one-second sample per RTT, each with the given movement rate on the route.</summary>
    private void SampleRoundTrips(double sentBytesPerSecond, params int[] roundTrips)
    {
        foreach (int roundTrip in roundTrips)
        {
            healthByRoute[Route] = new RouteHealth(roundTrip, RouteHealth.UnknownQuality, 0);
            backoff.ReportSent(Route, (int)sentBytesPerSecond);
            backoff.Advance(1f);
        }
    }
}
