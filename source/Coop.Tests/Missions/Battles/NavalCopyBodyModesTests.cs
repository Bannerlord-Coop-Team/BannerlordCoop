#if DEBUG
using Missions.Naval;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalCopyBodyModesTests
{
    private static readonly Guid Own = Guid.Parse("1d93665a-64a9-409b-8736-f77261b57865");
    private static readonly Guid Copy = Guid.Parse("2e78828e-627f-41e5-982f-76df24366941");
    private static readonly Guid OtherCopy = Guid.Parse("2f00aa11-0000-41e5-982f-76df24366941");

    private static readonly (Guid, bool)[] Registered = { (Own, true), (Copy, false), (OtherCopy, false) };

    private static CopyBodyRequest Parse(params string[] args)
    {
        Assert.Null(CopyBodyRequest.TryParse(args, out var request));
        return request;
    }

    [Theory]
    [InlineData("all", "kinematic", CopyBodyMode.Kinematic)]
    [InlineData("ALL", "Steered", CopyBodyMode.Steered)]
    [InlineData("2e78", "static", CopyBodyMode.Static)]
    public void TryParse_AcceptsTheThreeModes(string target, string mode, CopyBodyMode expected)
    {
        var request = Parse(target, mode);

        Assert.Equal(expected, request.Mode);
        Assert.Equal(target.Equals("all", StringComparison.OrdinalIgnoreCase), request.IsAll);
    }

    [Theory]
    [InlineData("dynamic")]
    [InlineData("")]
    [InlineData("kinematics")]
    public void TryParse_RefusesAnUnknownMode(string mode)
    {
        Assert.Contains("Unknown mode", CopyBodyRequest.TryParse(new[] { "all", mode }, out var request));
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_RefusesMissingArguments()
    {
        Assert.Equal(CopyBodyRequest.Usage, CopyBodyRequest.TryParse(new[] { "all" }, out _));
        Assert.NotNull(CopyBodyRequest.TryParse(new[] { " ", "steered" }, out _));
    }

    [Fact]
    public void TryParse_AppliesSteeredKnobs()
    {
        var request = Parse("all", "steered", "deadzone=0.5", "clamp=3", "tau=0.1", "snap=40");

        Assert.Equal(0.5f, request.Knobs.DeadZoneMetres);
        Assert.Equal(3f, request.Knobs.ClampMetresPerSecond);
        Assert.Equal(0.1f, request.Knobs.TauSeconds);
        Assert.Equal(40f, request.Knobs.SnapMetres);
    }

    [Theory]
    [InlineData("kinematic", "gain=2", "steered copies only")]
    [InlineData("static", "gain=2", "steered copies only")]
    [InlineData("steered", "gain=-1", "gain must be")]
    [InlineData("steered", "speed=1", "Unknown knob")]
    [InlineData("steered", "gain", "Expected key=value")]
    public void TryParse_RefusesBadKnobs(string mode, string knob, string reason)
    {
        Assert.Contains(reason, CopyBodyRequest.TryParse(new[] { "all", mode, knob }, out _));
    }

    [Fact]
    public void ResolveCopies_RefusesAnOwnHull()
    {
        var error = Parse("1d93", "steered").ResolveCopies(Registered, out var copies);

        Assert.Contains("own hull", error);
        Assert.Empty(copies);
    }

    [Fact]
    public void ResolveCopies_RefusesAnAmbiguousPrefix()
    {
        Assert.Contains("Several", Parse("2", "static").ResolveCopies(Registered, out _));
    }

    [Fact]
    public void ResolveCopies_AllNamesEveryCopyButNoOwnHull()
    {
        Assert.Null(Parse("all", "steered").ResolveCopies(Registered, out var copies));

        Assert.Equal(new[] { Copy, OtherCopy }, copies);
    }

    [Fact]
    public void ResolveCopies_KeepsAPrefixNoHullMatchesYet()
    {
        Assert.Null(Parse("abcd", "steered").ResolveCopies(Registered, out var copies));

        Assert.Empty(copies);
    }

    [Fact]
    public void Table_TracksASimulatingBodyAsSteeredAndADisabledOneAsKinematic()
    {
        var driver = new FakeDriver();
        driver.Simulating.Add("steered");
        var table = new CopyBodyModeTable<string>(driver);

        Assert.Equal(CopyBodyMode.Steered, table.Track("steered", Copy).Mode);
        Assert.NotNull(table.Entries["steered"].Steering);
        Assert.Equal(CopyBodyMode.Kinematic, table.Track("kinematic", OtherCopy).Mode);
    }

    [Fact]
    public void Table_BlocksSteeringAKinematicCopy()
    {
        var table = new CopyBodyModeTable<string>(new FakeDriver());
        table.Track("copy", Copy);
        table.AddRule(Parse("all", "steered"));

        table.ReconcileAll();

        var entry = table.Entries["copy"];
        Assert.Equal(CopyBodyMode.Kinematic, entry.Mode);
        Assert.Contains("cannot turn it back on", entry.Blocked);
    }

    [Fact]
    public void Table_MakesStaticAndRevertsToKinematic()
    {
        var driver = new FakeDriver();
        var table = new CopyBodyModeTable<string>(driver);
        table.Track("copy", Copy);

        table.AddRule(Parse("2e78", "static"));
        table.ReconcileAll();
        Assert.Equal(CopyBodyMode.Static, table.Entries["copy"].Mode);

        table.AddRule(Parse("2e78", "kinematic"));
        table.ReconcileAll();

        Assert.Equal(CopyBodyMode.Kinematic, table.Entries["copy"].Mode);
        Assert.Equal(new[] { "static:copy", "restore:copy" }, driver.Calls);
    }

    [Fact]
    public void Table_SteeredToStaticGoesThroughKinematic()
    {
        var driver = new FakeDriver();
        driver.Simulating.Add("copy");
        var table = new CopyBodyModeTable<string>(driver);
        table.Track("copy", Copy);

        table.AddRule(Parse("all", "static"));
        table.ReconcileAll();

        Assert.Equal(new[] { "disable:copy", "static:copy" }, driver.Calls);
        Assert.Equal(CopyBodyMode.Static, table.Entries["copy"].Mode);
    }

    [Fact]
    public void Table_AFailedStaticSwitchStaysKinematicAndIsNotRetried()
    {
        var driver = new FakeDriver { StaticError = "no body shape" };
        var table = new CopyBodyModeTable<string>(driver);
        table.Track("copy", Copy);
        table.AddRule(Parse("all", "static"));

        table.ReconcileAll();
        table.ReconcileAll();

        Assert.Equal(CopyBodyMode.Kinematic, table.Entries["copy"].Mode);
        Assert.Equal("no body shape", table.Entries["copy"].Blocked);
        Assert.Single(driver.Calls);
    }

    [Fact]
    public void Table_APrefixRuleOverridesAnEarlierAllRule()
    {
        var table = new CopyBodyModeTable<string>(new FakeDriver());
        table.AddRule(Parse("all", "static"));
        table.AddRule(Parse("2e78", "kinematic"));

        Assert.Equal(CopyBodyMode.Kinematic, table.ModeFor(Copy));
        Assert.Equal(CopyBodyMode.Static, table.ModeFor(OtherCopy));

        table.AddRule(Parse("all", "steered"));
        Assert.Single(table.Rules);
        Assert.Equal(CopyBodyMode.Steered, table.ModeFor(Copy));
    }

    [Fact]
    public void Table_RemovalRevertsTheCopyAndStopsTrackingIt()
    {
        var driver = new FakeDriver();
        driver.Simulating.Add("steered");
        var table = new CopyBodyModeTable<string>(driver);
        table.Track("steered", Copy);
        table.Track("static", OtherCopy);
        table.AddRule(Parse("2f00", "static"));
        table.AddRule(Parse("2e78", "steered"));
        table.ReconcileAll();

        table.OnRemoving("steered");
        table.OnRemoving("static");
        table.OnRemoving("never-tracked");

        Assert.Empty(table.Entries);
        Assert.Equal(new[] { "static:static", "disable:steered", "restore:static" }, driver.Calls);
    }

    [Fact]
    public void Table_ClearRevertsEveryCopyAndDropsTheRules()
    {
        var driver = new FakeDriver();
        var table = new CopyBodyModeTable<string>(driver);
        table.Track("copy", Copy);
        table.AddRule(Parse("all", "static"));
        table.ReconcileAll();

        table.Clear();

        Assert.Empty(table.Entries);
        Assert.Empty(table.Rules);
        Assert.Equal("restore:copy", driver.Calls.Last());
        Assert.Equal(CopyBodyMode.Kinematic, table.ModeFor(Copy));
    }

    private sealed class FakeDriver : ICopyBodyDriver<string>
    {
        public HashSet<string> Simulating { get; } = new HashSet<string>();
        public List<string> Calls { get; } = new List<string>();
        public string StaticError { get; set; }

        public bool IsSimulating(string hull) => Simulating.Contains(hull);

        public void DisableSimulation(string hull)
        {
            Simulating.Remove(hull);
            Calls.Add("disable:" + hull);
        }

        public string MakeStatic(string hull)
        {
            Calls.Add("static:" + hull);
            return StaticError;
        }

        public void RestoreFromStatic(string hull) => Calls.Add("restore:" + hull);
    }
}
#endif
