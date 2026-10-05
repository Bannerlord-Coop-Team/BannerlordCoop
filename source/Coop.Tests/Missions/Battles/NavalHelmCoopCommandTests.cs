#if DEBUG
using HarmonyLib;
using System;
using Missions.Naval;
using NavalDLC.Missions.ShipInput;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalHelmCoopCommandTests
{
    [Theory]
    [InlineData("0 1 1", 0f, 1f, 1, NavalHelmCoopCommand.DefaultSeconds)]
    [InlineData("-0.5 0.75 0 12", -0.5f, 0.75f, 0, 12)]
    [InlineData("1 -1 0 1", 1f, -1f, 0, 1)]
    [InlineData("-1 0 1 30", -1f, 0f, 1, 30)]
    public void TryParse_AcceptsAxesSailAndSeconds(string argLine, float rudder, float row, int sail, int seconds)
    {
        Assert.True(NavalHelmCoopCommand.TryParse(Args(argLine), out var request, out var error), error);

        Assert.Equal(rudder, request.Rudder);
        Assert.Equal(row, request.Row);
        Assert.Equal(sail, request.Sail);
        Assert.Equal(seconds, request.Seconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0 1")]
    [InlineData("0 1 1 5 extra")]
    [InlineData("1.5 0 0")]
    [InlineData("0 -1.01 0")]
    [InlineData("NaN 0 0")]
    [InlineData("left 0 0")]
    [InlineData("0 0 2")]
    [InlineData("0 0 full")]
    [InlineData("0 0 1 0")]
    [InlineData("0 0 1 31")]
    [InlineData("0 0 1 2.5")]
    public void TryParse_RejectsOutOfRangeOrMalformedArguments(string argLine)
    {
        Assert.False(NavalHelmCoopCommand.TryParse(Args(argLine), out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Theory]
    [InlineData(0f, 1f, 1, RowerLongitudinalInput.Forward, RowerLateralInput.None, 0f, SailInput.Full)]
    [InlineData(0f, -1f, 0, RowerLongitudinalInput.Backward, RowerLateralInput.None, 0f, SailInput.Raised)]
    [InlineData(1f, 0f, 0, RowerLongitudinalInput.None, RowerLateralInput.Right, 1f, SailInput.Raised)]
    [InlineData(-1f, 0f, 0, RowerLongitudinalInput.None, RowerLateralInput.Left, -1f, SailInput.Raised)]
    [InlineData(-0.5f, 1f, 0, RowerLongitudinalInput.Forward, RowerLateralInput.Left, -0.7f, SailInput.Raised)]
    [InlineData(0.1f, 0.1f, 0, RowerLongitudinalInput.None, RowerLateralInput.None, 0f, SailInput.Raised)]
    public void KeyboardRecord_MatchesTheControlViewsAxisMapping(float rudder, float row, int sail,
        RowerLongitudinalInput longitudinal, RowerLateralInput lateral, float rudderLateral, SailInput sailInput)
    {
        var record = NavalHelmCoopCommand.KeyboardRecord(rudder, row, sail);

        Assert.Equal(longitudinal, record.RowerLongitudinal);
        Assert.Equal(lateral, record.RowerLateral);
        Assert.Equal(RowerLongitudinalInput.None, record.RowerLongitudinalDoubleTap);
        Assert.Equal(rudderLateral, record.RudderLateral, 3);
        Assert.Equal(sailInput, record.Sail);
    }

    [Fact]
    public void OverridePatch_BindsToTheInstalledControlView()
    {
        var harmony = new Harmony("coop.tests.naval.helm_override");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(NavalHelmOverridePatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static string[] Args(string line) => line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
}
#endif
