#if DEBUG
using Missions.Naval;
using System;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalInspectSummaryTests
{
    [Fact]
    public void TryParseMode_DefaultsToTheFullOutput()
    {
        Assert.True(NavalInspectSummary.TryParseMode(new string[0], out bool summary, out var error), error);
        Assert.False(summary);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("brief")]
    [InlineData("SUMMARY")]
    public void TryParseMode_AcceptsSummaryOrBrief(string mode)
    {
        Assert.True(NavalInspectSummary.TryParseMode(new[] { mode }, out bool summary, out var error), error);
        Assert.True(summary);
    }

    [Theory]
    [InlineData("full")]
    [InlineData("summary extra")]
    public void TryParseMode_RejectsOtherArguments(string argLine)
    {
        Assert.False(NavalInspectSummary.TryParseMode(argLine.Split(' '), out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void FormatHull_PrintsTheKeyFieldsOfACopyOnOneLine()
    {
        var hull = new NavalInspectSummary.HullSummary("72da8e4a", "northern_light_ship", "testclient1", "testclient1", false,
            8700f, 9000f, 4500f, 4500f, "afloat", "steered", 0.123f, 0.8f, 1);

        Assert.Equal("72da8e4a northern_light_ship owner=testclient1 auth=testclient1 hp=8700/9000 sail=4500/4500 afloat " +
            "copy=steered drift=0.12/0.80 fatesSent=1", NavalInspectSummary.FormatHull(hull));
    }

    [Fact]
    public void FormatHull_LeavesOutDriftAndFatesItHasNot()
    {
        var hull = new NavalInspectSummary.HullSummary("7b246ef3", "eastern_galley", "testclient2", "testclient1", true,
            17000.4f, 20000f, 0f, 10000f, "sinking", "own", null, null, null);

        Assert.Equal("7b246ef3 eastern_galley owner=testclient2(npc) auth=testclient1 hp=17000.4/20000 sail=0/10000 sinking copy=own",
            NavalInspectSummary.FormatHull(hull));
    }

    [Fact]
    public void ShortId_IsTheFirstEightHexDigits()
    {
        Assert.Equal("72da8e4a", NavalInspectSummary.ShortId(Guid.Parse("72da8e4a-0000-4000-8000-000000000001")));
    }
}
#endif
