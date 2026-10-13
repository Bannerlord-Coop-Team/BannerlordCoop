using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Station use across a host migration: the old host seats and releases an AI crew member, the new host adopts it and
/// numbers its own uses from one, and an observer applies both owners' sequences (R31). A removed hull is never re-seated.
/// </summary>
public class NavalStationUseOwnerChangeScenarioTests : NavalBattleTestEnvironment
{
    public NavalStationUseOwnerChangeScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void StationUse_SeatReleaseMigrateReseat_FollowsEachOwnersRevisions()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var a = battle["A"];
        var b = battle["B"];
        var c = battle["C"];
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        b.RegisterAgent(crewId, "A");
        var crewOnC = c.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        var helmOnA = a.Hull(pirate).Station("helm");

        a.UseStation(crewOnA, helmOnA, inUse: true);
        Assert.Same(crewOnC, c.Hull(pirate).SeatedAt("helm"));
        a.UseStation(crewOnA, helmOnA, inUse: false);
        Assert.Null(c.Hull(pirate).SeatedAt("helm"));
        a.UseStation(crewOnA, helmOnA, inUse: true);
        Assert.Same(crewOnC, c.Hull(pirate).SeatedAt("helm"));

        battle.MigrateHost(a, "B");
        b.Ticks(3);
        Assert.True(b.Agents.TryGetAgentInfo(crewId, out var adopted));
        Assert.Equal("B", adopted.CurrentAuthority);
        Assert.True(c.Agents.TryGetAgentInfo(crewId, out var observed));
        Assert.Equal("B", observed.CurrentAuthority);
        Assert.Equal("B", c.Ship(pirate).CurrentAuthority);
        Assert.Same(crewOnC, c.Hull(pirate).SeatedAt("helm"));

        // The new host's first revisions are below the old host's last one; an observer still applies them.
        var helmOnB = b.Hull(pirate).Station("helm");
        b.UseStation(adopted.Agent, helmOnB, inUse: false);
        Assert.Null(c.Hull(pirate).SeatedAt("helm"));
        b.UseStation(adopted.Agent, helmOnB, inUse: true);
        Assert.Same(crewOnC, c.Hull(pirate).SeatedAt("helm"));

        // The observer's copy is pooled and vanilla released the puppet locally; nothing seats it there again.
        c.Hull(pirate).IsPooled = true;
        c.Hull(pirate).Seats.Clear();
        c.Tick();
        b.UseStation(adopted.Agent, helmOnB, inUse: false);
        b.UseStation(adopted.Agent, helmOnB, inUse: true);
        c.Tick();

        Assert.Null(c.Hull(pirate).SeatedAt("helm"));
        Assert.Equal(1, (long)c.StationStats()["removedHullSeats"]!);
    }
}
