using Missions.Messages;
using ProtoBuf;
using System;
using System.IO;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleRopeStateTests
{
    [Fact]
    public void Sample_RoundTripsTheRopeStates()
    {
        var ropes = new[]
        {
            NavalRopesTests.Rope(3, BattleRopeState.BridgeConnected),
            NavalRopesTests.Rope(1, BattleRopeState.RopeThrown, "4:attachment_machine_2"),
        };
        ropes[1].CurveTarget = new[] { 1f, 2f, 3f };
        ropes[1].CurveAngle = 35f;
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), "peer", 9, 1234,
            NetworkBattleShipSample.FromFrame(MatrixFrame.Identity), new BattleShipInput(0, 1, 0, 0.25f, 2), ropes);

        var copy = RoundTrip(sample);

        Assert.Equal(2, copy.Ropes.Length);
        for (int index = 0; index < ropes.Length; index++)
        {
            var expected = ropes[index];
            var actual = copy.Ropes[index];
            Assert.Equal(expected.SourceKey, actual.SourceKey);
            Assert.Equal(expected.Generation, actual.Generation);
            Assert.Equal(expected.State, actual.State);
            Assert.Equal(expected.TargetShipId, actual.TargetShipId);
            Assert.Equal(expected.TargetKey, actual.TargetKey);
            Assert.Equal(expected.Length, actual.Length);
            Assert.Equal(expected.HookFrame, actual.HookFrame);
            Assert.Equal(expected.CurveTarget, actual.CurveTarget);
            Assert.Equal(expected.CurveAngle, actual.CurveAngle);
            Assert.Equal(expected.PlankFlight, actual.PlankFlight);
            Assert.Equal(expected.DecorationPlanks, actual.DecorationPlanks);
            Assert.True(actual.IsValid);
        }
    }

    [Fact]
    public void Sample_WithoutRopes_RoundTripsAsNull()
    {
        var sample = new NetworkBattleShipSample(Guid.NewGuid(), "peer", 1, 1234, NetworkBattleShipSample.FromFrame(MatrixFrame.Identity));

        Assert.Null(RoundTrip(sample).Ropes);
        Assert.True(BattleRopeState.AreValid(null));
    }

    [Fact]
    public void FinalRopes_RoundTrip()
    {
        var final = new NetworkBattleRopeFinal(Guid.NewGuid(), "peer", new[] { NavalRopesTests.Rope(2, BattleRopeState.Removed) });

        var copy = RoundTrip(final);

        Assert.Equal(final.ShipId, copy.ShipId);
        Assert.Equal(final.OwnerControllerId, copy.OwnerControllerId);
        Assert.Equal(BattleRopeState.Removed, Assert.Single(copy.Ropes).State);
    }

    [Fact]
    public void AreValid_RejectsTwoStatesForOneStation()
    {
        var ropes = new[] { NavalRopesTests.Rope(1, BattleRopeState.Removed), NavalRopesTests.Rope(2, BattleRopeState.RopeThrown) };

        Assert.False(BattleRopeState.AreValid(ropes));
    }

    [Fact]
    public void AreValid_RejectsMoreStationsThanAHullHas()
    {
        var ropes = Enumerable.Range(0, BattleRopeState.MaxRopesPerHull + 1)
            .Select(index => NavalRopesTests.Rope(1, BattleRopeState.RopeThrown, index + ":attachment_machine"))
            .ToArray();

        Assert.False(BattleRopeState.AreValid(ropes));
    }

    private static T RoundTrip<T>(T value)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, value);
        stream.Position = 0;
        return Serializer.Deserialize<T>(stream);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("state")]
    [InlineData("pulling_without_target")]
    [InlineData("plank_without_flight")]
    [InlineData("target_without_key")]
    [InlineData("hook_frame")]
    [InlineData("length")]
    public void IsValid_RejectsStatesAPeerCannotRebuild(string defect)
    {
        var rope = NavalRopesTests.Rope(1, BattleRopeState.RopesPulling);
        switch (defect)
        {
            case "generation": rope.Generation = 0; break;
            case "state": rope.State = 6; break;
            case "pulling_without_target": rope.TargetShipId = Guid.Empty; rope.TargetKey = null; break;
            case "plank_without_flight": rope.State = BattleRopeState.BridgeThrown; break;
            case "target_without_key": rope.TargetKey = null; break;
            case "hook_frame": rope.HookFrame = new float[3]; break;
            case "length": rope.Length = float.NaN; break;
        }

        Assert.False(rope.IsValid);
    }
}
