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
        Assert.Equal(new[] { "solo", "vs_pirates", "boarding", "ramming", "sinking", "migration", "late_join", "fleet", "duel", "allied_npc" },
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
            "delay=7", "reconnect=30");

        Assert.Equal(new[] { "PlayerThree", "PlayerOne" }, config.ControllerIds);
        Assert.Equal("PlayerThree", config.InitiatorControllerId);
        Assert.Equal(NavalScenarioPlacement.Boarding, config.Placement);
        Assert.Equal(12.5f, config.Spacing);
        Assert.Equal(2, config.EnemyHulls);
        Assert.Equal("dromon_ship", config.HullId);
        Assert.Equal(2, config.ShipsPerPlayer);
        Assert.Equal(7f, config.DelaySeconds);
        Assert.Equal(30f, config.ReconnectSeconds);
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

        Assert.Equal(1, Resolve("solo").EnemyHulls);
        Assert.Equal(10f, Resolve("boarding").Spacing);
        Assert.Equal(NavalScenarioPlacement.Ramming, Resolve("ramming").Placement);
        Assert.True(Resolve("sinking").SinkingEnemyHull);
        Assert.True(Resolve("migration").DisconnectBattleHost);
        Assert.Equal(3, Resolve("fleet").ShipsPerPlayer);
        Assert.True(Resolve("allied_npc").AlliedNpc);

        var duel = Resolve("duel");
        Assert.True(duel.Duel);
        Assert.Equal(0, duel.EnemyHulls);

        var lateJoin = Resolve("late_join");
        Assert.True(lateJoin.LateJoin);
        Assert.Equal("PlayerTwo", lateJoin.LateJoinerControllerId);
        Assert.Null(Resolve("vs_pirates").LateJoinerControllerId);
    }

    [Theory]
    [InlineData("Unknown preset nope; presets: solo, vs_pirates, boarding, ramming, sinking, migration, late_join, fleet, duel, allied_npc.", "nope")]
    [InlineData("hulls must be a whole number from 1 to 8.", "vs_pirates", "hulls=9")]
    [InlineData("hulls must be a whole number from 1 to 8.", "vs_pirates", "hulls=x")]
    [InlineData("duel has no AI enemy, so it takes no hulls knob.", "duel", "hulls=2")]
    [InlineData("vs_pirates keeps the vanilla deployment, so it takes no spacing knob.", "vs_pirates", "spacing=10")]
    [InlineData("spacing must be a number of metres from 1 to 200.", "boarding", "spacing=0")]
    [InlineData("ships must be a whole number from 1 to 4.", "fleet", "ships=5")]
    [InlineData("delay must be a number of seconds from 1 to 600.", "migration", "delay=NaN")]
    [InlineData("reconnect must be a number of seconds from 5 to 600.", "migration", "reconnect=1")]
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
