using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// The <c>migration</c> preset mid-battle: the battle host leaves, its AI hulls move to the promoted host, which parks,
/// replaces and re-boards each over the fake engine and streams it at the new epoch. A failed replacement hands the
/// parked agents back.
/// </summary>
public class NavalHostMigrationScenarioTests : NavalBattleTestEnvironment
{
    public NavalHostMigrationScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void Migration_MovesTheAiHullsToTheNewHost_WhichSwapsAndStreamsThemAtTheNewEpoch()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirates);
        var parkedCrew = SeatCrewOnCopy(b, pirates[0]);
        battle.TickAll(5);
        var lateSampleFromA = a.SentSamples(pirates[0]).Last();
        var copy = b.Hull(pirates[0]);

        battle.MigrateHost(a, "B");
        Assert.All(pirates, pirate => Assert.Equal("B", b.Ship(pirate).CurrentAuthority));
        Assert.All(pirates, pirate => Assert.Equal("B", c.Ship(pirate).CurrentAuthority));

        b.Tick();
        Assert.Empty(copy.Seats);
        Assert.Empty(b.Engine.Boardings);

        b.Tick();
        var fresh = b.Hull(pirates[0]);
        Assert.NotSame(copy, fresh);
        Assert.True(copy.IsRemoved);
        Assert.False(fresh.IsCopy);
        Assert.Empty(b.Engine.Boardings);

        b.Tick();
        Assert.Contains(b.Engine.Boardings, boarding =>
            ReferenceEquals(boarding.Hull, fresh.Hull) && ReferenceEquals(boarding.Agents.Single().Agent, parkedCrew));
        Assert.All(pirates, pirate => Assert.False(b.Hull(pirate).IsCopy));

        // The new host numbers the AI samples from one again; the observer follows it at the newer epoch.
        b.Hull(pirates[0]).Frame = new MatrixFrame(Mat3.Identity, new Vec3(42f, 300f, 0f));
        b.Tick();
        c.Tick();
        var newHostSample = b.SentSamples(pirates[0]).Last();
        Assert.True(newHostSample.Sequence < lateSampleFromA.Sequence);
        Assert.Equal(battle.HostEpoch, newHostSample.HostEpoch);
        Assert.Equal(newHostSample.Sequence, (long)c.Stream(pirates[0])["accepted"]!);
        Assert.Equal(42f, c.Hull(pirates[0]).Frame.origin.x, 3);

        c.Call(() => c.Instance.SimulateMessage(a.Network.NetPeer, lateSampleFromA));
        Assert.Equal("not_authority", (string)c.Stream(pirates[0])["lastReject"]!);
        Assert.Equal(newHostSample.Sequence, (long)c.Stream(pirates[0])["accepted"]!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Migration_WhenTheHullCannotBeReplaced_HandsTheParkedAgentsBack(bool replaceThrows)
    {
        using var battle = StartPirateBattle(out var a, out var b, out _, out var pirates);
        var parkedCrew = SeatCrewOnCopy(b, pirates[0]);
        b.Engine.ReplaceHullFails = !replaceThrows;
        b.Engine.ReplaceHullThrows = replaceThrows;
        var copy = b.Hull(pirates[0]);

        battle.MigrateHost(a, "B");
        b.Ticks(4);

        Assert.Contains(b.Engine.Boardings, boarding =>
            ReferenceEquals(boarding.Hull, copy.Hull) && ReferenceEquals(boarding.Agents.Single().Agent, parkedCrew));
        Assert.Same(copy, b.Hull(pirates[0]));
        Assert.Equal("B", b.Ship(pirates[0]).CurrentAuthority);
        Assert.Equal(pirates.Length, b.Engine.Boardings.Count);
        b.Ticks(3);
        Assert.Equal(pirates.Length, b.Engine.Boardings.Count);
    }

    // vs_pirates for three players; the battle host A simulates two AI hulls.
    private NavalBattle StartPirateBattle(out NavalPeer a, out NavalPeer b, out NavalPeer c, out Guid[] pirates)
    {
        var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        a = battle["A"];
        b = battle["B"];
        c = battle["C"];
        var host = a;
        pirates = Enumerable.Range(0, 2)
            .Select(i => battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f)))
            .ToArray();
        battle.CommitAll();
        battle.TickAll();
        return battle;
    }

    // A crew member of the departing host seated at the copy's helm, so the swap has an agent to park.
    private static Agent SeatCrewOnCopy(NavalPeer peer, Guid shipId)
    {
        var crew = peer.RegisterAgent(Guid.NewGuid(), "A");
        var copy = peer.Hull(shipId);
        copy.Seats[copy.Station("helm")] = crew;
        return crew;
    }
}
