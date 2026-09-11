#if DEBUG
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Newtonsoft.Json.Linq;
using ProtoBuf;
using System;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public sealed class NavalLabMeasurementTests
{
    [Fact]
    public void Probe_ExpiresAtThirtySecondsAndDuplicateCannotExtendOrRestart()
    {
        var measurement = new NavalLabMeasurement();
        var id = Guid.NewGuid();
        Assert.Equal("applied", measurement.Begin(id, 1, 100));
        Assert.True(measurement.Active(129.99));
        Assert.Equal("duplicate:not_restarted", measurement.Begin(id, 1, 129));
        Assert.Equal("rejected:probe_active", measurement.Begin(Guid.NewGuid(), 1, 129));
        Assert.False(measurement.Active(130));
        Assert.Equal("duplicate:not_restarted", measurement.Begin(id, 1, 131));
        Assert.False(measurement.Active(131));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Probe_RejectsUnreadyOrRecoveryEpoch(int epoch)
    {
        var measurement = new NavalLabMeasurement();
        Assert.StartsWith("rejected:", measurement.Begin(Guid.NewGuid(), epoch, 0));
        Assert.Equal(Guid.Empty, measurement.OperationId);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Probe_RejectsNonFiniteTime(double time) =>
        Assert.StartsWith("rejected:", new NavalLabMeasurement().Begin(Guid.NewGuid(), 1, time));

    [Theory]
    [InlineData("hold")]
    [InlineData("stop")]
    [InlineData("epoch_changed")]
    [InlineData("disposed")]
    public void Cancellation_IsTerminalForTheOperation(string reason)
    {
        var measurement = new NavalLabMeasurement();
        var id = Guid.NewGuid();
        measurement.Begin(id, 1, 0);
        measurement.Cancel(reason);
        Assert.False(measurement.Active(1));
        Assert.Equal("duplicate:not_restarted", measurement.Begin(id, 1, 1));
        Assert.Equal("cancelled:" + reason, (string?)JObject.FromObject(measurement.Read(0))["status"]);
    }

    [Fact]
    public void Capacity_RetainsSixtyFourSamplesAndPagesEightWithExplicitOverwrites()
    {
        var measurement = new NavalLabMeasurement();
        measurement.Begin(Guid.NewGuid(), 1, 0);
        var values = new float[24];
        for (int i = 1; i <= 70; i++) measurement.Add(i, i + 10, i + 20, i + 21, i + 22, values, null!, "unavailable:test");
        values[0] = 123;
        var first = JObject.FromObject(measurement.Read(0));
        Assert.Equal(6, (int)first["overwritten"]!);
        var samples = first["samples"]!;
        Assert.Equal(8, samples.Count());
        Assert.Equal(7, (long)samples[0]!["sequence"]!);
        Assert.Equal(17, (long)samples[0]!["hostSampleCallback"]!);
        Assert.Equal(27, (long)samples[0]!["receivedCallback"]!);
        Assert.Equal(28, (long)samples[0]!["appliedCallback"]!);
        Assert.Equal(29, (long)samples[0]!["observedCallback"]!);
        Assert.Equal(0, (float)samples[0]!["transmittedFrames"]![0]!);
        Assert.Equal(JTokenType.Null, samples[0]!["native"]!.Type);
        Assert.Equal(15, (long)JObject.FromObject(measurement.Read(14))["samples"]![0]!["sequence"]!);
    }

    [Fact]
    public void ProbeBudget_DoesNotGrowUnboundedAfterCancellation()
    {
        var measurement = new NavalLabMeasurement();
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal("applied", measurement.Begin(Guid.NewGuid(), 1, i));
            measurement.Cancel("test");
        }
        Assert.Equal("rejected:probe_budget", measurement.Begin(Guid.NewGuid(), 1, 9));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Sample_RejectsNonFiniteTransmittedAndObservedScalars(float invalid)
    {
        var measurement = new NavalLabMeasurement();
        measurement.Begin(Guid.NewGuid(), 1, 0);
        var values = new float[24];
        values[23] = invalid;
        Assert.Throws<ArgumentException>(() => measurement.Add(1, 1, null, null, 1, values, null!, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabAgentSnapshot(Guid.NewGuid(), "A", true,
            Vec3.Zero, 100, 80, "hull", true, 0, 1, new Vec3(invalid, 0, 0)));
    }

    [Fact]
    public void Codec_RetainsSourceCallbackAndProbeIdentityWithoutNativeValues()
    {
        var message = new NetworkNavalLabFrames(Guid.NewGuid(), 1, 23, new float[24], 456, Guid.NewGuid());
        var copy = Serializer.DeepClone(message);
        Assert.Equal(message.IncarnationId, copy.IncarnationId);
        Assert.Equal(message.Epoch, copy.Epoch);
        Assert.Equal(message.Sequence, copy.Sequence);
        Assert.Equal(message.SourceCallback, copy.SourceCallback);
        Assert.Equal(message.ProbeOperationId, copy.ProbeOperationId);
        Assert.Equal(message.Frames, copy.Frames);
    }

    [Fact]
    public void UnavailableAndSupersededSamples_DoNotInventAnObservedCut()
    {
        var measurement = new NavalLabMeasurement();
        Assert.Equal("unavailable:not_started", (string?)JObject.FromObject(measurement.Read(0))["status"]);
        measurement.Begin(Guid.NewGuid(), 1, 0);
        measurement.Add(1, 20, 30, 31, null, new float[24], null!, "superseded_before_next_mission_callback");
        var row = JObject.FromObject(measurement.Read(0))["samples"]![0]!;
        Assert.Equal(JTokenType.Null, row["observedCallback"]!.Type);
        Assert.Equal(JTokenType.Null, row["native"]!.Type);
        Assert.Contains("superseded", (string?)row["error"]);
    }
}
#endif
