using Missions.Agents.Handlers;
using Missions.Agents.Packets;
using Newtonsoft.Json.Linq;
using System;
using Xunit;

namespace Coop.Tests.Missions.Agents;

/// <summary>Checks per-recipient window measurements and receiver rejection budgets without native agents.</summary>
public class NavalDeckMovementProbeTests
{
    private readonly Guid agent = Guid.NewGuid();
    private readonly Guid ship = Guid.NewGuid();

    // Separate recipients must not produce zero intervals when the same pose is sent to both.
    [Fact]
    public void Heartbeats_AreMeasuredPerRecipientWithTheirOwnIdleIntervals()
    {
        var probe = new NavalDeckMovementProbe();
        foreach (float time in new[] { 0f, 0.25f, 0.5f })
        {
            probe.Record("a", agent, Pose(0f), true, time);
            probe.Record("b", agent, Pose(0f), true, time);
        }
        var rows = (JArray)View(probe, 0.5f)["agents"]!;
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            Assert.Equal(3, (int)row["count"]!);
            Assert.Equal(2, (int)row["idleIntervalCount"]!);
            Assert.Equal(0.25f, (float)row["idleMeanIntervalSeconds"]!);
            Assert.Equal(0.25f, (float)row["maxIntervalSeconds"]!);
        }
    }

    // Moving endpoints and hull changes cannot inflate the idle interval sample count.
    [Fact]
    public void IdleIntervals_ExcludeMovingEndpointsAndHullTransitions()
    {
        var probe = new NavalDeckMovementProbe();
        probe.Record("a", agent, Pose(0f), true, 0f);
        probe.Record("a", agent, Pose(2f), true, 0.1f);
        probe.Record("a", agent, Pose(0f), true, 0.2f);
        probe.Record("a", agent, Pose(0f), true, 0.45f);
        var otherShip = Pose(0f);
        otherShip.CaptureDeck(Guid.NewGuid(), default, 0f);
        probe.Record("a", agent, otherShip, true, 0.7f);
        var row = Assert.Single((JArray)View(probe, 0.7f)["agents"]!);
        Assert.Equal(3, (int)row["intervalCount"]!);
        Assert.Equal(1, (int)row["idleIntervalCount"]!);
        Assert.Equal(0.25f, (float)row["idleMeanIntervalSeconds"]!, 4);
        Assert.Equal(2f, (float)row["maxSpeed"]!);
    }

    // Expiry must work even if an agent stops sending entirely; a lone timestamp gives no interval.
    [Fact]
    public void Window_ExpiresSamplesAndDoesNotMeasureAcrossItsBoundary()
    {
        var probe = new NavalDeckMovementProbe();
        probe.Record("a", agent, Pose(0f), true, 0f);
        probe.Record("a", agent, Pose(0f), true, 9f);
        var row = Assert.Single((JArray)View(probe, 11f)["agents"]!);
        Assert.Equal(1, (int)row["count"]!);
        Assert.Equal(JTokenType.Null, row["meanIntervalSeconds"]!.Type);
        Assert.Empty((JArray)View(probe, 20f)["agents"]!);
    }

    // AI-only deck statistics cannot retain a former deck or player sample as a fresh baseline.
    [Fact]
    public void NonDeckNonAiAndRecipientDeparture_RemoveTheirMeasurements()
    {
        var probe = new NavalDeckMovementProbe();
        probe.Record("a", agent, Pose(0f), true, 0f);
        probe.Record("b", agent, Pose(0f), true, 0f);
        probe.Record("a", agent, default, true, 1f);
        Assert.Single((JArray)View(probe, 1f)["agents"]!);
        probe.Record("b", agent, Pose(0f), false, 1f);
        Assert.Empty((JArray)View(probe, 1f)["agents"]!);
        probe.Record("a", agent, Pose(0f), true, 2f);
        probe.RemoveRecipient("a");
        Assert.Empty((JArray)View(probe, 2f)["agents"]!);
    }

#if DEBUG
    // A fault is consumed only by its selected agent and ends exactly at the requested count.
    [Fact]
    public void RejectionBudget_IsSelectedBoundedReplaceableAndClearedOnTeardown()
    {
        var probe = new NavalDeckMovementProbe();
        probe.RejectNext(agent, 2);
        Assert.False(probe.ShouldReject(Guid.NewGuid()));
        Assert.True(probe.ShouldReject(agent));
        Assert.True(probe.ShouldReject(agent));
        Assert.False(probe.ShouldReject(agent));
        Assert.Equal(2, (int)View(probe, 0f)["rejection"]!["injected"]!);
        probe.RejectNext(agent, 10);
        probe.RejectNext(Guid.Empty, 0);
        Assert.False(probe.ShouldReject(agent));
        probe.RejectNext(agent, 10);
        probe.Record("a", agent, Pose(0f), true, 0f);
        probe.Reset();
        Assert.False(probe.ShouldReject(agent));
        Assert.Empty((JArray)View(probe, 0f)["agents"]!);
        Assert.Equal(0, (int)View(probe, 0f)["rejection"]!["injected"]!);
    }
#endif

    // Supplies a deck sample with a controlled deck-relative speed.
    private AgentData Pose(float speed)
    {
        var data = new AgentData();
        data.CaptureDeck(ship, default, speed);
        return data;
    }

    // Reads the same JSON-compatible diagnostic object exposed by naval inspect.
    private JObject View(NavalDeckMovementProbe probe, float time) => JObject.FromObject(probe.Inspect(time));
}
