#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

public class NavalScenarioResolverTests
{
    private static readonly string[] Connected = { "PlayerOne", "PlayerTwo", "PlayerThree" };

    private static NavalScenarioConfig Resolve(params string[] args)
    {
        Assert.True(new NavalScenarioResolver().TryResolve(args, Connected, out var config, out var error), error);
        return config;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(new NavalScenarioResolver().TryResolve(args, Connected, out var config, out var error));
        Assert.Null(config);
        return error;
    }

    [Fact]
    public void Presets_HaveUniqueNamesAndResolveWithEveryConnectedPlayer()
    {
        var names = NavalScenarioPresets.All.Select(preset => preset.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        Assert.Equal(new[] { "solo", "vs_pirates", "boarding", "ramming", "sinking", "migration", "late_join", "fleet", "duel", "allied_npc", "retreat",
                "encounter", "blockade" },
            names);

        foreach (var name in names)
        {
            var config = Resolve(name);
            Assert.Equal(name, config.Preset);
            Assert.NotEmpty(config.ScenarioId);
            Assert.Equal("", config.MapEventId);
            Assert.All(config.ControllerIds, id => Assert.Contains(id, Connected));
        }
    }

    /// <summary>R53 keeps a successor and an observer in the battle while the original host leaves.</summary>
    [Fact]
    public void Migration_StagesThreeClientsWithGracefulHostDeparture()
    {
        var clients = new[] { "testclient1", "testclient2", "testclient3" };
        Assert.True(new NavalScenarioResolver().TryResolve(
            new[] { "migration", "drop=leave", "delay=120", "weapons=2", "testclient1", "testclient2", "testclient3" },
            clients, out var config, out var error), error);
        Assert.Equal(clients, config.ControllerIds);
        Assert.True(config.GracefulDisconnect);
        Assert.Equal("testclient1", config.DisconnectingControllerId("testclient1"));
        Assert.Equal(new[] { "testclient2", "testclient3" }, config.ControllerIds.Where(id => id != config.DisconnectingControllerId("testclient1")));
    }

    [Fact]
    public void Resolve_WithoutIdsTakesTheConnectedPlayersInOrderUpToThePresetMaximum()
    {
        Assert.Equal(Connected, Resolve("vs_pirates").ControllerIds);
        Assert.Equal(new[] { "PlayerOne" }, Resolve("solo").ControllerIds);
        Assert.Equal(new[] { "PlayerOne", "PlayerTwo" }, Resolve("duel").ControllerIds);
    }

    [Fact]
    public void Resolve_KeepsTheListedPlayersInTheirOrderAndReadsKnobsAnywhere()
    {
        var config = Resolve("boarding", "PlayerThree", "spacing=12.5", "PlayerOne", "hulls=2", "hull=dromon_ship", "ships=2",
            "delay=7", "reconnect=30", "weapons=2");

        Assert.Equal(new[] { "PlayerThree", "PlayerOne" }, config.ControllerIds);
        Assert.Equal("PlayerThree", config.InitiatorControllerId);
        Assert.Equal(NavalScenarioPlacement.Boarding, config.Placement);
        Assert.Equal(12.5f, config.Spacing);
        Assert.Equal(2, config.EnemyHulls);
        Assert.Equal("dromon_ship", config.HullId);
        Assert.Equal(2, config.ShipsPerPlayer);
        Assert.Equal(7f, config.DelaySeconds);
        Assert.Equal(30f, config.ReconnectSeconds);
        Assert.Equal(2, config.ArmedShipsPerParty);
    }

    // M10: the ramming preset fits a ram on one ship of every party unless ram=n says otherwise.
    [Fact]
    public void Resolve_RamDefaultsToOneShipForRamming_AndTheKnobOverridesIt()
    {
        Assert.Equal(1, Resolve("ramming").RammedShipsPerParty);
        Assert.Equal(0, Resolve("ramming", "ram=0").RammedShipsPerParty);
        Assert.Equal(0, Resolve("vs_pirates").RammedShipsPerParty);
        Assert.Equal(3, Resolve("fleet", "ram=3").RammedShipsPerParty);
    }

    // R48: reserve=n is off by default and takes any preset, duel and boarding included.
    [Fact]
    public void Resolve_ReserveDefaultsToNone_AndTheKnobSetsIt()
    {
        Assert.Equal(0, Resolve("boarding").ReserveTroops);
        Assert.Equal(20, Resolve("boarding", "reserve=20").ReserveTroops);
        Assert.Equal(15, Resolve("duel", "reserve=15").ReserveTroops);
    }

    // The forced-failure knob is opt-in and reaches clients over the real scenario message contract.
    [Fact]
    public void FleetFailure_IsOptIn_AndRoundTripsToTheBattleHost()
    {
        Assert.False(Resolve("vs_pirates").FailNpcFleet);
        Assert.False(Resolve("vs_pirates", "fleet_fail=0").FailNpcFleet);
        var config = Resolve("vs_pirates", "fleet_fail=1");
        using var stream = new System.IO.MemoryStream();
        ProtoBuf.Serializer.Serialize(stream, config);
        stream.Position = 0;
        Assert.True(ProtoBuf.Serializer.Deserialize<NavalScenarioConfig>(stream).FailNpcFleet);
        Assert.Equal("fleet_fail must be 0 or 1.", Refuse("vs_pirates", "fleet_fail=yes"));
    }

    [Fact]
    public void Resolve_CopiesThePresetValues()
    {
        var vsPirates = Resolve("vs_pirates");
        Assert.Equal(1, vsPirates.ShipsPerPlayer);
        Assert.Equal(3, vsPirates.TroopTemplatesPerPlayer);
        Assert.Equal(3, vsPirates.EnemyHulls);
        Assert.Equal(NavalScenarioPresets.DefaultHullId, vsPirates.HullId);
        Assert.Equal(NavalScenarioPlacement.Deployment, vsPirates.Placement);
        Assert.Equal(NavalScenarioPresets.DefaultDelaySeconds, vsPirates.DelaySeconds);
        Assert.Equal(NavalScenarioPresets.DefaultReconnectSeconds, vsPirates.ReconnectSeconds);
        Assert.Equal(0, vsPirates.ArmedShipsPerParty);
        Assert.Equal(1, Resolve("vs_pirates", "weapons=1").ArmedShipsPerParty);

        Assert.Equal(1, Resolve("solo").EnemyHulls);
        Assert.Equal(10f, Resolve("boarding").Spacing);
        Assert.Equal(NavalScenarioPlacement.Ramming, Resolve("ramming").Placement);
        Assert.True(Resolve("sinking").SinkingEnemyHull);
        Assert.True(Resolve("migration").DisconnectBattleHost);
        Assert.False(Resolve("migration").DisconnectNonHost);
        Assert.True(Resolve("migration", "who=nonhost").DisconnectNonHost);
        Assert.False(Resolve("migration", "who=host").DisconnectNonHost);
        Assert.False(Resolve("migration").GracefulDisconnect);
        Assert.True(Resolve("migration", "drop=leave").GracefulDisconnect);
        Assert.False(Resolve("migration", "drop=hard").GracefulDisconnect);
        Assert.Equal(3, Resolve("fleet").ShipsPerPlayer);
        Assert.True(Resolve("allied_npc").AlliedNpc);

        var duel = Resolve("duel");
        Assert.Equal(NavalScenarioEntry.Duel, duel.Entry);
        Assert.Equal(0, duel.EnemyHulls);

        var lateJoin = Resolve("late_join");
        Assert.True(lateJoin.LateJoin);
        Assert.Equal("PlayerTwo", lateJoin.LateJoinerControllerId);
        Assert.Null(Resolve("vs_pirates").LateJoinerControllerId);

        var retreat = Resolve("retreat");
        Assert.True(retreat.RetreatSecondPlayer);
        Assert.Equal("PlayerTwo", retreat.RetreatingControllerId);
        Assert.Null(Resolve("vs_pirates").RetreatingControllerId);
    }

    [Theory]
    [InlineData("Unknown preset nope; presets: solo, vs_pirates, boarding, ramming, sinking, migration, late_join, fleet, duel, allied_npc, retreat, encounter, blockade.", "nope")]
    [InlineData("hulls must be a whole number from 1 to 8.", "vs_pirates", "hulls=9")]
    [InlineData("hulls must be a whole number from 1 to 8.", "vs_pirates", "hulls=x")]
    [InlineData("duel has no AI enemy, so it takes no hulls knob.", "duel", "hulls=2")]
    [InlineData("vs_pirates keeps the vanilla deployment, so it takes no spacing knob.", "vs_pirates", "spacing=10")]
    [InlineData("spacing must be a number of metres from 1 to 200.", "boarding", "spacing=0")]
    [InlineData("ships must be a whole number from 1 to 4.", "fleet", "ships=5")]
    [InlineData("weapons must be a whole number from 0 to 4.", "vs_pirates", "weapons=5")]
    [InlineData("weapons must be a whole number from 0 to 4.", "vs_pirates", "weapons=-1")]
    [InlineData("ram must be a whole number from 0 to 4.", "ramming", "ram=5")]
    [InlineData("ram must be a whole number from 0 to 4.", "ramming", "ram=x")]
    [InlineData("reserve must be a whole number from 0 to 200.", "boarding", "reserve=201")]
    [InlineData("reserve must be a whole number from 0 to 200.", "duel", "reserve=x")]
    [InlineData("delay must be a number of seconds from 1 to 600.", "migration", "delay=NaN")]
    [InlineData("reconnect must be a number of seconds from 5 to 600.", "migration", "reconnect=1")]
    [InlineData("who must be host or nonhost.", "migration", "who=client")]
    [InlineData("vs_pirates disconnects nobody, so it takes no who knob.", "vs_pirates", "who=nonhost")]
    [InlineData("drop must be hard or leave.", "migration", "drop=soft")]
    [InlineData("vs_pirates disconnects nobody, so it takes no drop knob.", "vs_pirates", "drop=hard")]
    [InlineData("hull must name a ship hull id.", "solo", "hull=")]
    [InlineData("Player PlayerFour is not connected; coop.debug.players.list shows the current ids.", "vs_pirates", "PlayerFour")]
    [InlineData("Player PlayerOne is listed twice.", "vs_pirates", "PlayerOne", "PlayerOne")]
    [InlineData("solo takes at most 1 players, got 2.", "solo", "PlayerOne", "PlayerTwo")]
    [InlineData("migration needs at least 2 connected players, has 1.", "migration", "PlayerTwo")]
    public void Resolve_RefusesInvalidRequests(string expected, params string[] args)
    {
        Assert.Equal(expected, Refuse(args));
    }

    [Fact]
    public void Resolve_EncounterAndBlockadeKeepTheVanillaDeploymentAndTheirEntryPath()
    {
        Assert.Equal(NavalScenarioEntry.StartBattle, Resolve("vs_pirates").Entry);
        var encounter = Resolve("encounter");
        Assert.Equal(NavalScenarioEntry.Encounter, encounter.Entry);
        Assert.Equal(NavalScenarioPlacement.Deployment, encounter.Placement);
        Assert.Equal(Connected, encounter.ControllerIds);

        var blockade = Resolve("blockade", "town=town_EN1", "hulls=4");
        Assert.Equal(NavalScenarioEntry.Blockade, blockade.Entry);
        Assert.Equal("town_EN1", blockade.BlockadeTownId);
        Assert.Equal(4, blockade.EnemyHulls);
        Assert.Equal("", Resolve("blockade").BlockadeTownId);
    }

    [Fact]
    public void Resolve_OnlyTheBlockadeTakesATown()
    {
        Assert.Contains("no town knob", Refuse("encounter", "town=town_EN1"));
        Assert.Contains("no town knob", Refuse("vs_pirates", "town=town_EN1"));
        Assert.Contains("coastal town", Refuse("blockade", "town= "));
        Assert.Contains("no spacing knob", Refuse("encounter", "spacing=10"));
    }

    [Fact]
    public void Resolve_RefusesAnUnknownKnobAndAnEmptyRequest()
    {
        Assert.StartsWith("Unknown knob speed.", Refuse("vs_pirates", "speed=3"));
        Assert.Equal(NavalScenarioResolver.Usage, Refuse());
    }

    [Fact]
    public void Resolve_RefusesAPresetThatNeedsMorePlayersThanAreConnected()
    {
        Assert.False(new NavalScenarioResolver().TryResolve(new[] { "late_join" }, new[] { "PlayerOne" }, out _, out var error));
        Assert.Equal("late_join needs at least 2 connected players, has 1.", error);
    }
}
#endif
