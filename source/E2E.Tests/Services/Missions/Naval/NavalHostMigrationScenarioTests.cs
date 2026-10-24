using GameInterface.Services.MapEvents.TroopSupply;
using TaleWorlds.CampaignSystem;
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
        var registeredBeforeSwap = b.RegisteredShipIds();

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
        // The replacement raised the spawn event too; it rebinds the ship's id and registers no own hull.
        Assert.Equal(registeredBeforeSwap, b.RegisteredShipIds());

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
        battle.ExpectLoggedFailure(replaceThrows ? "failed in phase ReplaceHull" : "the hull could not be replaced");
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

    // K5: the old host's queued AI crew left with it. The promoted host recovers those troops from the roster (the
    // reinforcement fielder's recovery, stubbed here) and queues them on the AI hulls once each is replaced by a simulated one,
    // up to each hull's capacity; until then they keep the pirate side alive as pending reserve, and afterwards its samples
    // report them to every client.
    [Fact]
    public void Migration_TheNewHostQueuesTheRecoveredAiReserveOnTheHullsItNowSimulates()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirates);
        foreach (var pirate in pirates)
        {
            a.Hull(pirate).SpawnableReserve = 10;
            b.Hull(pirate).CrewCapacity = 8;
        }
        battle.TickAll(2);
        Assert.All(pirates, pirate => Assert.Equal(10, (int)c.Stream(pirate)["reserve"]!));

        battle.MigrateHost(a, "B");
        b.Call(() => b.RecoveredReserve.AddRange(Enumerable.Range(1, 15).Select(seed => new CoopAgentOrigin(
            (CharacterObject)Game.Current.PlayerTroop, null, 0, null, new UniqueTroopDescriptor(seed), battle.PirateMapEventPartyId))));
        b.Tick();

        var whileSwapping = EndTally(b, BattleSideEnum.Defender, liveHumans: 0);
        Assert.All(pirates, pirate => Assert.Equal(0, b.Hull(pirate).SpawnableReserve));
        Assert.Equal(15, whileSwapping.Reserve);
        Assert.False(whileSwapping.IsDepleted);

        b.Ticks(3);
        Assert.All(pirates, pirate => Assert.False(b.Hull(pirate).IsCopy));
        Assert.Equal(new[] { 8, 7 }, pirates.Select(pirate => b.Hull(pirate).SpawnableReserve));
        Assert.Equal(15, EndTally(b, BattleSideEnum.Defender, liveHumans: 0).Reserve);

        battle.TickAll(2);
        Assert.Equal(new[] { 8, 7 }, pirates.Select(pirate => (int)c.Stream(pirate)["reserve"]!));
    }

    // M6 D1: every copy of an AI hull, and the hull the promoted host swaps in for its copy, keeps the campaign ship id the
    // host's record carried, so whoever simulates the hull can report that ship's fate.
    [Fact]
    public void Migration_CopiesAndTheSwappedInHull_KeepTheCampaignShipId()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirates);
        Assert.All(pirates, pirate =>
        {
            Assert.Equal(CampaignShipIdOf(pirate, pirates), b.Hull(pirate).CampaignShipId);
            Assert.Equal(CampaignShipIdOf(pirate, pirates), c.Hull(pirate).CampaignShipId);
        });

        battle.MigrateHost(a, "B");
        b.Ticks(3);

        Assert.All(pirates, pirate =>
        {
            Assert.False(b.Hull(pirate).IsCopy);
            Assert.Equal(CampaignShipIdOf(pirate, pirates), b.Hull(pirate).CampaignShipId);
            b.Call(() => Assert.Equal(CampaignShipIdOf(pirate, pirates), b.Engine.CampaignShipIdOf(b.Ship(pirate).Hull)));
        });
    }

    private static string CampaignShipIdOf(Guid pirate, Guid[] pirates) => $"Ship_pirate_{Array.IndexOf(pirates, pirate)}";
    // vs_pirates for three players; the battle host A simulates two AI hulls, each standing for a pirate campaign ship.
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
            .Select(i => battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f),
                campaignShipId: $"Ship_pirate_{i}"))
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
