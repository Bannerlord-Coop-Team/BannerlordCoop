#if DEBUG
using Common.Commands;
using Missions.Battles;
using Missions.Naval;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public sealed class NavalLabSnapshotTests
{
    [Fact]
    public void InspectCommand_SerializesInitializedShapeWithoutEngineValuesOrCustomSettings()
    {
        var frame = new MatrixFrame(new Mat3(new Vec3(0, 1, 0), new Vec3(-1, 0, 0), Vec3.Up), new Vec3(250, 310, 2));
        var ships = Enumerable.Range(0, 2).Select(_ => new NavalLabShipSnapshot(Guid.NewGuid(), frame,
            new Vec3(1, 2, 3), new Vec3(4, 5, 6), 400, new Vec3(7, 8, 9), true, false, 5)).ToArray();
        var agents = Enumerable.Range(0, 10).Select(i => new NavalLabAgentSnapshot(Guid.NewGuid(),
            i < 5 ? "A" : "B", i % 5 == 0, new Vec3(i, 250, 3), 100, 80, "ship_drakkar", true, i / 5, 123)).ToArray();
        var snapshot = new
        {
            native = new { startup = new { phase = "fixture_initialized" }, ships, agents },
            recoverySupported = false
        };
        var coordinator = Mock.Of<INavalLabCoordinator>(value => value.Inspect() == snapshot);
        var result = new NavalLabInspectCommand(coordinator).ProcessCommand(Mock.Of<ICoopCommandArgs>());
        Assert.True(result.Succeeded);
        Assert.StartsWith("LIVE_TEST_JSON=", result.Output);
        var json = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
        Assert.Equal(2, json["native"]!["ships"]!.Count());
        Assert.Equal(10, json["native"]!["agents"]!.Count());
        Assert.Equal(2, json["native"]!["agents"]!.Count(agent => (bool)agent["nativeCaptain"]!));
        var ship = json["native"]!["ships"]![0]!;
        Assert.Equal(250f, (float)ship["frame"]!["origin"]!["x"]!);
        Assert.Equal(310f, (float)ship["frame"]!["origin"]!["y"]!);
        Assert.Equal(1f, (float)ship["frame"]!["side"]!["y"]!);
        Assert.Equal(-1f, (float)ship["frame"]!["forward"]!["x"]!);
        Assert.Equal(1f, (float)ship["frame"]!["up"]!["z"]!);
        Assert.Equal(3f, (float)ship["velocity"]!["z"]!);
        Assert.Equal(4f, (float)ship["angularVelocity"]!["x"]!);
        Assert.Equal(400f, (float)ship["crewMass"]!);
        Assert.Equal(8f, (float)ship["crewWeightedPosition"]!["y"]!);
        Assert.Equal(5, (int)ship["deckFrames"]!);
        Assert.Null(ship["error"]!.Value<string>());
        Assert.Equal(1, (int)json["native"]!["agents"]![9]!["steppedFixtureSlot"]!);
        Assert.All(json.Descendants().OfType<JValue>().Where(value => value.Type == JTokenType.Float),
            value => Assert.True(double.IsFinite(value.Value<double>())));
        Assert.All(((JObject)ship["frame"]!).Properties(), property =>
            Assert.Equal(new[] { "x", "y", "z" }, ((JObject)property.Value).Properties().Select(item => item.Name)));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Snapshot_RejectsNonFinitePhysicalObservations(float invalid)
    {
        var frame = MatrixFrame.Identity;
        frame.rotation.f.z = invalid;
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabFrameSnapshot(frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabVectorSnapshot(new Vec3(invalid, 0, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabVectorSnapshot(new Vec3(0, invalid, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabVectorSnapshot(new Vec3(0, 0, invalid)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabShipSnapshot(Guid.NewGuid(), MatrixFrame.Identity,
            Vec3.Zero, Vec3.Zero, invalid, Vec3.Zero, true, false, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabAgentSnapshot(Guid.NewGuid(), "A", true,
            Vec3.Zero, invalid, 80, null!, false, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabAgentSnapshot(Guid.NewGuid(), "A", true,
            Vec3.Zero, 100, invalid, null!, false, -1, 0));
    }

    [Fact]
    public void CrewActionSnapshot_SerializesNativeChannelsWithoutEngineObjects()
    {
        var value = new NavalLabActionSnapshot(1, 42, "Stand", 0.25f, 0.75f, "None");
        var json = JObject.Parse(JsonConvert.SerializeObject(value));
        Assert.Equal(1, (int)json["Channel"]!);
        Assert.Equal(42, (int)json["ActionIndex"]!);
        Assert.Equal(0.25f, (float)json["Progress"]!);
        Assert.Equal(0.75f, (float)json["Weight"]!);
        Assert.Equal("None", (string?)json["AnimationFlags"]);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void CrewActionSnapshot_RejectsNonFiniteProgressAndWeight(float invalid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabActionSnapshot(0, 1, "Stand", invalid, 1, "None"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NavalLabActionSnapshot(0, 1, "Stand", 0, invalid, "None"));
    }

    [Fact]
    public void UnavailableSnapshot_PreservesIdentityWithoutFabricatedZeroMeasurements()
    {
        var id = Guid.NewGuid();
        var json = JObject.Parse(JsonConvert.SerializeObject(new NavalLabShipSnapshot(id, "invalid_ship_entity")));
        Assert.Equal(id, (Guid)json["id"]!);
        Assert.Equal("invalid_ship_entity", (string?)json["error"]);
        Assert.Equal(JTokenType.Null, json["frame"]!.Type);
        Assert.Equal(JTokenType.Null, json["crewMass"]!.Type);
        Assert.Equal(JTokenType.Null, json["activeSimulation"]!.Type);
    }
}
#endif
