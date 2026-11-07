using Common.Logging;
using Common.Network.Session;
using Serilog;
using System;
using System.Collections.Generic;

namespace Missions.Agents.Handlers;

public interface IMovementRouteBackoff
{
    /// <summary>Ceiling the route's delivery health allows; infinite until the route first backs off.</summary>
    double GetBytesPerSecond(string controllerId);

    /// <summary>Counts movement bytes handed to a route, the rate a back-off is measured from.</summary>
    void ReportSent(string controllerId, int bytes);

    /// <summary>Re-evaluates every route's ceiling once per sample interval.</summary>
    void Advance(float elapsedSeconds);

    /// <summary>Forgets a route, so a returning peer starts over with a fresh baseline.</summary>
    void RemoveRoute(string controllerId);

    /// <summary>Forgets every route.</summary>
    void Clear();
}

/// <summary>Keeps one movement ceiling per route: cuts fast when delivery health degrades, recovers slowly.</summary>
public sealed class MovementRouteBackoff : IMovementRouteBackoff
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementRouteBackoff>();

    private const float SampleIntervalSeconds = 1f;
    // One lost LiteNetLib pong lifts the averaged RTT about this far for two samples, so a real rise needs a third.
    private const int RoundTripRiseMilliseconds = 300;
    private const int ConfirmSamples = 3;
    private const float MinimumQuality = 0.75f;
    private const long MaximumQueuedBytes = 1024 * 1024;
    private const double CutFactor = 0.7d;
    // A queue takes time to drain after a cut, so wait before judging it and hold while the RTT is still falling.
    private const int SamplesBetweenCuts = 2;
    private const double DrainingRoundTripRatio = 0.9d;
    // Below this, movement cannot be what saturates a link, so a route is never cut under it.
    internal const double FloorBytesPerSecond = 128 * 1024;
    private const int SamplesBeforeAcceptingRoundTrip = 10;
    private const int RecoveryCooldownSamples = 5;
    internal const double GrowthBytesPerSecondPerSample = 16 * 1024;
    private const double GrowthUtilization = 0.8d;

    /// <summary>One route's ceiling plus what its next decision needs from the samples before it.</summary>
    private sealed class RouteState
    {
        public double BytesPerSecond = double.PositiveInfinity;
        public long SentBytes;
        public int BaselineRoundTripMilliseconds;
        public int CongestedSamples;
        public int HealthySamples;
        public int SamplesSinceCut = SamplesBetweenCuts;
        public int RoundTripAtLastCut;
        public int MinimalTrafficSamples;
    }

    private readonly IBattleNetwork network;
    private readonly Dictionary<string, RouteState> routes =
        new Dictionary<string, RouteState>(StringComparer.Ordinal);
    private float sampleElapsed;

    /// <summary>Reads route health from the mission network, the only side that knows each peer's transport.</summary>
    public MovementRouteBackoff(IBattleNetwork network)
    {
        this.network = network;
    }

    public double GetBytesPerSecond(string controllerId) =>
        routes.TryGetValue(controllerId, out RouteState route)
            ? route.BytesPerSecond
            : double.PositiveInfinity;

    public void ReportSent(string controllerId, int bytes)
    {
        if (!routes.TryGetValue(controllerId, out RouteState route))
        {
            route = new RouteState();
            routes.Add(controllerId, route);
        }

        route.SentBytes += bytes;
    }

    public void Advance(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f) return;

        sampleElapsed += elapsedSeconds;
        if (sampleElapsed < SampleIntervalSeconds) return;

        foreach (KeyValuePair<string, RouteState> pair in routes)
            Sample(pair.Key, pair.Value, sampleElapsed);
        sampleElapsed = 0f;
    }

    public void RemoveRoute(string controllerId) => routes.Remove(controllerId);

    public void Clear()
    {
        routes.Clear();
        sampleElapsed = 0f;
    }

    /// <summary>Folds one health reading and the rate sent since the last one into the route's ceiling.</summary>
    private void Sample(string controllerId, RouteState route, float elapsedSeconds)
    {
        double sentBytesPerSecond = route.SentBytes / elapsedSeconds;
        route.SentBytes = 0;
        if (!network.TryGetRouteHealth(controllerId, out RouteHealth health))
        {
            // The peer fell back to the relay, which has no reading. It likely shares the uplink that just
            // saturated, so ramp up rather than reset, and a returning peer's RTT starts a fresh baseline.
            route.BaselineRoundTripMilliseconds = 0;
            Recover(route, sentBytesPerSecond);
            return;
        }

        route.SamplesSinceCut++;
        if (health.RoundTripMilliseconds > 0 &&
            (route.BaselineRoundTripMilliseconds == 0 ||
                health.RoundTripMilliseconds < route.BaselineRoundTripMilliseconds))
        {
            route.BaselineRoundTripMilliseconds = health.RoundTripMilliseconds;
        }

        if (IsCongested(route, health))
            HandleCongested(controllerId, route, health, sentBytesPerSecond);
        else
            Recover(route, sentBytesPerSecond);
    }

    /// <summary>A route is congested once its RTT rises off its best, delivery drops, or sends back up.</summary>
    private static bool IsCongested(RouteState route, RouteHealth health)
    {
        if (route.BaselineRoundTripMilliseconds > 0 &&
            health.RoundTripMilliseconds - route.BaselineRoundTripMilliseconds >= RoundTripRiseMilliseconds)
        {
            return true;
        }

        if (health.Quality != RouteHealth.UnknownQuality && health.Quality < MinimumQuality) return true;

        return health.QueuedBytes > MaximumQueuedBytes;
    }

    /// <summary>Cuts a confirmed congested route from what it actually sends, unless the last cut is still working.</summary>
    private static void HandleCongested(
        string controllerId,
        RouteState route,
        RouteHealth health,
        double sentBytesPerSecond)
    {
        route.HealthySamples = 0;
        route.CongestedSamples++;
        if (route.CongestedSamples < ConfirmSamples) return;

        double effectiveBytesPerSecond = Math.Min(route.BytesPerSecond, sentBytesPerSecond);
        if (effectiveBytesPerSecond <= FloorBytesPerSecond)
        {
            AcceptRoundTripIfUnchanged(controllerId, route, health);
            return;
        }

        route.MinimalTrafficSamples = 0;
        if (route.SamplesSinceCut < SamplesBetweenCuts) return;

        route.SamplesSinceCut = 0;
        if (route.RoundTripAtLastCut > 0 &&
            health.RoundTripMilliseconds <= route.RoundTripAtLastCut * DrainingRoundTripRatio)
        {
            route.RoundTripAtLastCut = health.RoundTripMilliseconds;
            return;
        }

        route.RoundTripAtLastCut = health.RoundTripMilliseconds;
        route.BytesPerSecond = Math.Max(FloorBytesPerSecond, effectiveBytesPerSecond * CutFactor);
        Logger.Information(
            "[BattleTraffic] Route {ControllerId} backed off to {BytesPerSecond:0} bytes/s: sending " +
            "{SentBytesPerSecond:0} bytes/s, rtt={RoundTrip}ms baseline={Baseline}ms quality={Quality:0.00} " +
            "queuedBytes={QueuedBytes}",
            controllerId,
            route.BytesPerSecond,
            sentBytesPerSecond,
            health.RoundTripMilliseconds,
            route.BaselineRoundTripMilliseconds,
            health.Quality,
            health.QueuedBytes);
    }

    /// <summary>Takes a lasting RTT as the new baseline once movement is too small to be its cause, like a route switch.</summary>
    private static void AcceptRoundTripIfUnchanged(string controllerId, RouteState route, RouteHealth health)
    {
        route.MinimalTrafficSamples++;
        if (route.MinimalTrafficSamples < SamplesBeforeAcceptingRoundTrip) return;

        route.MinimalTrafficSamples = 0;
        route.BaselineRoundTripMilliseconds = Math.Max(0, health.RoundTripMilliseconds);
        Logger.Information(
            "[BattleTraffic] Route {ControllerId} kept rtt={RoundTrip}ms with movement at its minimum, using it as the baseline",
            controllerId,
            health.RoundTripMilliseconds);
    }

    /// <summary>Grows a healthy route's ceiling after a cooldown, only while movement is actually using it.</summary>
    private static void Recover(RouteState route, double sentBytesPerSecond)
    {
        route.CongestedSamples = 0;
        route.MinimalTrafficSamples = 0;
        route.RoundTripAtLastCut = 0;
        route.HealthySamples++;
        if (route.HealthySamples <= RecoveryCooldownSamples) return;
        if (sentBytesPerSecond < route.BytesPerSecond * GrowthUtilization) return;

        route.BytesPerSecond += GrowthBytesPerSecondPerSample;
    }
}
