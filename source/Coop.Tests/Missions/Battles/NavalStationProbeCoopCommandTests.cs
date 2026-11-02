#if COOP_NAVAL
using Common.Commands;
using Missions.Naval;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Checks station command boundaries and the capture format without loading a native mission.</summary>
public class NavalStationProbeCoopCommandTests
{
    /// <summary>Rejects incomplete modes, extra input, signed indices and out-of-range watch durations.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("snapshot a extra")]
    [InlineData("watch")]
    [InlineData("watch a")]
    [InlineData("watch a 0")]
    [InlineData("watch a 301")]
    [InlineData("watch a NaN")]
    [InlineData("watch a 1.5")]
    [InlineData("watch a -1")]
    [InlineData("reseat a -1")]
    [InlineData("reseat a +1")]
    [InlineData("reseat a 2147483648")]
    [InlineData("reseat a 1 extra")]
    [InlineData("release a 1")]
    public void ProcessCommand_RejectsMalformedInputBeforeMissionAccess(string line)
    {
        var probe = new Mock<INavalStationProbe>(MockBehavior.Strict);
        var result = new NavalStationProbeCoopCommand(probe.Object).ProcessCommand(Args(line));
        Assert.False(result.Succeeded);
        Assert.Equal(NavalStationProbeCoopCommand.Usage, result.Output);
        probe.VerifyNoOtherCalls();
    }

    /// <summary>Dispatches exactly one validated request for each supported command form.</summary>
    [Theory]
    [InlineData("snapshot", "snapshot", null, 0)]
    [InlineData("SNAPSHOT a46d53f1", "snapshot", "a46d53f1", 0)]
    [InlineData("watch a46d53f1 1", "watch", "a46d53f1", 1)]
    [InlineData("watch a46d53f1 300", "watch", "a46d53f1", 300)]
    [InlineData("reseat a46d53f1 0", "reseat", "a46d53f1", 0)]
    [InlineData("reseat a46d53f1 2147483647", "reseat", "a46d53f1", int.MaxValue)]
    public void ProcessCommand_DispatchesValidatedMode(string line, string mode, string? prefix, int value)
    {
        var probe = new Mock<INavalStationProbe>(MockBehavior.Strict);
        var expected = new CoopCommandResult(true, "captured");
        probe.Setup(item => item.Execute(mode, prefix, value)).Returns(expected);
        var result = new NavalStationProbeCoopCommand(probe.Object).ProcessCommand(Args(line));
        Assert.Equal(expected, result);
        probe.Verify(item => item.Execute(mode, prefix, value), Times.Once);
        probe.VerifyNoOtherCalls();
    }

    /// <summary>Rejects blank required tokens even when the dispatcher preserves them.</summary>
    [Fact]
    public void TryParse_RejectsMissingAndBlankTokens()
    {
        Assert.False(NavalStationProbeCoopCommand.TryParse(null!, out _, out _, out _));
        Assert.False(NavalStationProbeCoopCommand.TryParse(new[] { " " }, out _, out _, out _));
        Assert.False(NavalStationProbeCoopCommand.TryParse(new[] { "snapshot", " " }, out _, out _, out _));
        Assert.False(NavalStationProbeCoopCommand.TryParse(new[] { "watch", " ", "10" }, out _, out _, out _));
    }

    /// <summary>Preserves network identities, numeric coordinates and explicit unknown native flags in JSON.</summary>
    [Fact]
    public void SnapshotJson_PreservesDiagnosticShapeAcrossCultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var id = Guid.Parse("e95bddc7-ef55-4444-8881-3a78f915a37d");
            var hull = Guid.Parse("a46d53f1-c4ed-4a19-8db0-03533514d06a");
            var sample = new StationProbeSample
            {
                AgentId = id, AgentIndex = 7, HullId = hull, Authority = "copy",
                Station = hull + "/13:oars_holder/0:oar_deck#0",
                WorldPosition = NavalStationProbeLogic.Vector(new Vec3(1.25f, 2.5f, 1.089f)),
                HullLocalPosition = NavalStationProbeLogic.Vector(new Vec3(0f, 0f, 0.155f)),
                PinnedThisFrame = true, ReseatCount = 1,
            };
            var json = JObject.Parse(JsonConvert.SerializeObject(sample));
            Assert.Equal(id, Guid.Parse(json.Value<string>("agentId")!));
            Assert.Equal(hull, Guid.Parse(json.Value<string>("hullId")!));
            Assert.Equal("copy", json.Value<string>("authority"));
            Assert.EndsWith("#0", json.Value<string>("station"));
            Assert.Equal(1.25f, json["worldPosition"]!.Value<float>("x"));
            Assert.Equal(0.155f, json["hullLocalPosition"]!.Value<float>("z"));
            Assert.True(json.Value<bool>("pinnedThisFrame"));
            Assert.Equal(1, json.Value<int>("reseatCount"));
            foreach (var name in new[] { "userFrame", "animationDisplacement", "expectedSpawnPilotPosition", "steppedShipId",
                "steppedEntity", "steppedRootEntity", "nativeParent", "actions", "excludedFromGravity", "physicsForceClosed", "faceGroupExclusion" })
                Assert.Equal(JTokenType.Null, json[name]!.Type);
            Assert.Contains("no getters", json.Value<string>("unavailable"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    /// <summary>Exports an ordered event/sample window with enough metadata to identify truncation.</summary>
    [Fact]
    public void CaptureJson_EvictsOldestRecordsAndKeepsMetadata()
    {
        var hull = Guid.Parse("a46d53f1-c4ed-4a19-8db0-03533514d06a");
        var capture = new StationProbeCapture(hull, "testclient1", 10f, 60);
        for (int index = 0; index <= StationProbeCapture.Capacity; index++)
            capture.Add(new { kind = index % 2 == 0 ? "snapshot" : "release", time = 10f + index, index });
        var json = JObject.Parse(capture.ToJson("elapsed", 70f));
        Assert.Equal(1, json.Value<int>("schemaVersion"));
        Assert.Equal(hull, Guid.Parse(json.Value<string>("hullId")!));
        Assert.Equal("testclient1", json.Value<string>("controller"));
        Assert.Equal(10f, json.Value<float>("startedAt"));
        Assert.Equal(70f, json.Value<float>("endedAt"));
        Assert.Equal(60, json.Value<int>("requestedSeconds"));
        Assert.Equal(5, json.Value<int>("sampleEveryFrames"));
        Assert.Equal(1, json.Value<int>("overwritten"));
        Assert.Equal("elapsed", json.Value<string>("reason"));
        var records = (JArray)json["records"]!;
        Assert.Equal(StationProbeCapture.Capacity, records.Count);
        Assert.Equal(1, records[0].Value<int>("index"));
        Assert.Equal(StationProbeCapture.Capacity, records[records.Count - 1].Value<int>("index"));
    }

    /// <summary>Models the ordinary naval command argument wrapper without invoking its dispatcher.</summary>
    private ICoopCommandArgs Args(string line)
    {
        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var args = new Mock<ICoopCommandArgs>();
        args.SetupGet(value => value.Count).Returns(values.Length);
        for (int index = 0; index < values.Length; index++)
        {
            int captured = index;
            args.SetupGet(value => value[captured]).Returns(values[index]);
        }
        return args.Object;
    }
}
#endif
