#if COOP_NAVAL
using Missions.Messages;
using Missions.Naval;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalWaterProbeTests
{
    [Fact]
    public void TryParse_DefaultsToTheProbeSpec()
    {
        Assert.Null(NavalWaterProbeSettings.TryParse(Array.Empty<string>(), out var settings));

        Assert.Equal(5, settings.Grid);
        Assert.Equal(40f, settings.Spacing);
        Assert.Equal(120f, settings.Seconds);
    }

    [Fact]
    public void TryParse_TakesTheKnobs()
    {
        Assert.Null(NavalWaterProbeSettings.TryParse(new[] { "grid=3", "spacing=12.5", "seconds=30" }, out var settings));

        Assert.Equal(3, settings.Grid);
        Assert.Equal(12.5f, settings.Spacing);
        Assert.Equal(30f, settings.Seconds);
    }

    [Theory]
    [InlineData("grid=0")]
    [InlineData("grid=16")]
    [InlineData("spacing=0")]
    [InlineData("seconds=-1")]
    [InlineData("hz=60")]
    [InlineData("grid")]
    public void TryParse_RefusesBadKnobs(string knob)
    {
        Assert.NotNull(NavalWaterProbeSettings.TryParse(new[] { knob }, out var settings));
        Assert.Null(settings);
    }

    [Fact]
    public void GridPoints_IsASquareCentredOnTheSceneCentre()
    {
        var points = NavalWaterProbeLogic.GridPoints(new Vec2(100f, 200f), 3, 10f);

        Assert.Equal(9, points.Length);
        Assert.Equal(90f, points[0].x);
        Assert.Equal(190f, points[0].y);
        Assert.Equal(100f, points[4].x);
        Assert.Equal(200f, points[4].y);
        Assert.Equal(110f, points[8].x);
        Assert.Equal(210f, points[8].y);
    }

    [Fact]
    public void Tick_IsATaggedJsonLineWithEveryClockHeightAndHull()
    {
        var hull = new NavalWaterProbeHull("1d93665a", "copy", "steered", 512.34567f, -20.5f, 135.1234f, 0.123456f, -1.5f, 2.25f, 0.5f);

        var line = NavalWaterProbeLogic.Tick("abcd1234", 7, 9, 0.016666f, 12.5f, 13f, 140.25f, 1700000000123L,
            new[] { 0.12345f, -0.5f }, new[] { hull });

        Assert.StartsWith("NAVAL_WATER_PROBE {", line);
        var json = JObject.Parse(line.Substring(NavalWaterProbeLogic.Tag.Length));
        Assert.Equal("tick", (string)json["t"]);
        Assert.Equal("abcd1234", (string)json["probe"]);
        Assert.Equal(7, (long)json["tick"]);
        Assert.Equal(9, (long)json["frame"]);
        Assert.Equal(12.5, (double)json["mt"]);
        Assert.Equal(1700000000123L, (long)json["utc"]);
        Assert.Equal(new[] { 0.1235, -0.5 }, json["h"].ToObject<double[]>());
        var probed = json["hulls"][0];
        Assert.Equal("1d93665a", (string)probed["id"]);
        Assert.Equal("copy", (string)probed["role"]);
        Assert.Equal("steered", (string)probed["mode"]);
        Assert.Equal(512.346, (double)probed["x"]);
        Assert.Equal(-20.5, (double)probed["y"]);
        Assert.Equal(135.123, (double)probed["yaw"]);
        Assert.Equal(0.1235, (double)probed["z"]);
        Assert.Equal(-1.5, (double)probed["pitch"]);
        Assert.Equal(2.25, (double)probed["roll"]);
    }

    // R45 roll: each hull logs the crew load its physics takes, so owner and copy loads can be compared offline.
    [Fact]
    public void Tick_LogsEachHullsCrewLoadAndItsSource()
    {
        var loaded = new NavalWaterProbeHull("1d93665a", "copy", "steered", 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            BattleShipCrewLoad.Create(480.04f, new Vec3(0.5f, -4.12345f, 1.5f)), "owner");
        var empty = new NavalWaterProbeHull("2e04776b", "own", "own", 0f, 0f, 0f, 0f, 0f, 0f, 0f, null, "vanilla");

        var line = NavalWaterProbeLogic.Tick("abcd1234", 1, 1, 0.016666f, 1f, 1f, 1f, 1L, new[] { 0f }, new[] { loaded, empty });

        var hulls = JObject.Parse(line.Substring(NavalWaterProbeLogic.Tag.Length))["hulls"];
        Assert.Equal(480.0, (double)hulls[0]["crewMass"]);
        Assert.Equal(new[] { 0.5, -4.123, 1.5 }, hulls[0]["crewLocal"].ToObject<double[]>());
        Assert.Equal("owner", (string)hulls[0]["crewSrc"]);
        Assert.Equal(0.0, (double)hulls[1]["crewMass"]);
        Assert.Equal(JTokenType.Null, hulls[1]["crewLocal"].Type);
        Assert.Equal("vanilla", (string)hulls[1]["crewSrc"]);
    }

    [Fact]
    public void Header_CarriesTheGridAndEnvironment()
    {
        Assert.Null(NavalWaterProbeSettings.TryParse(new[] { "grid=1" }, out var settings));

        var line = NavalWaterProbeLogic.Header("abcd1234", "PlayerOne", "mapevent_1", settings, new[] { new Vec2(1f, 2f) },
            new Dictionary<string, object> { ["waterQuality"] = 2f });

        var json = JObject.Parse(line.Substring(NavalWaterProbeLogic.Tag.Length));
        Assert.Equal("header", (string)json["t"]);
        Assert.Equal("PlayerOne", (string)json["controller"]);
        Assert.Equal(1, (int)json["grid"]);
        Assert.Equal(new[] { 1.0, 2.0 }, json["points"][0].ToObject<double[]>());
        Assert.Equal(2.0, (double)json["env"]["waterQuality"]);
    }

    [Fact]
    public void End_ReportsTheTickCountAndReason()
    {
        var json = JObject.Parse(NavalWaterProbeLogic.End("abcd1234", 42, "elapsed").Substring(NavalWaterProbeLogic.Tag.Length));

        Assert.Equal("end", (string)json["t"]);
        Assert.Equal(42, (long)json["ticks"]);
        Assert.Equal("elapsed", (string)json["reason"]);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1f, 90f)]
    [InlineData(2f, 90f)]
    [InlineData(-0.5f, -30f)]
    public void AxisTiltDegrees_IsTheAxisAngleAboveTheHorizon(float axisZ, float degrees)
    {
        Assert.Equal(degrees, NavalWaterProbeLogic.AxisTiltDegrees(axisZ), 3);
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0f)]
    [InlineData(0f, 1f, 0f, 90f)]
    [InlineData(-1f, 0f, 0f, 180f)]
    [InlineData(0f, -1f, 0f, -90f)]
    [InlineData(0.5f, 0.5f, 0.7f, 45f)]
    public void YawDegrees_IsTheForwardAxisHeadingOnTheWaterPlane(float x, float y, float z, float degrees)
    {
        Assert.Equal(degrees, NavalWaterProbeLogic.YawDegrees(new Vec3(x, y, z)), 3);
    }

    [Fact]
    public void StartRefusal_RefusesStartUntilDeploymentHasFinished()
    {
        Assert.NotNull(NavalWaterProbeCoopCommand.StartRefusal(deploymentFinished: false));
        Assert.Null(NavalWaterProbeCoopCommand.StartRefusal(deploymentFinished: true));
    }
}
#endif
