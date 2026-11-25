using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class TroopSelectionRulesTests
{
    private const string Hero = "player-hero";

    private readonly TroopSelectionRules rules = new(Mock.Of<IObjectManager>());
    private readonly Dictionary<string, int> healthy = new() { [Hero] = 1, ["recruit"] = 4, ["archer"] = 2 };

    [Fact]
    public void TryAccept_PutsTheHeroFirstWhetherOrNotItWasPicked()
    {
        Assert.True(rules.TryAccept(healthy, Hero, 5,
            new[] { new HideoutTroopSelectionEntry("recruit", 3), new HideoutTroopSelectionEntry(Hero, 1) },
            out var accepted, out var escorts));

        Assert.Equal(new[] { new HideoutTroopSelectionEntry(Hero, 1), new HideoutTroopSelectionEntry("recruit", 3) },
            accepted);
        Assert.Equal(3, escorts);
    }

    [Theory]
    [InlineData("recruit", 2, "recruit", 3)] // more of one troop than the party has healthy
    [InlineData("recruit", 3, "archer", 2)]  // more escorts than the allowance
    [InlineData(Hero, 2, "recruit", 1)]      // the hero twice
    [InlineData("recruit", 1, "knight", 1)]  // a troop the party doesn't have
    public void TryAccept_RejectsASquadThePartyCantField(string first, int firstCount, string second, int secondCount)
    {
        Assert.False(rules.TryAccept(healthy, Hero, 4,
            new[] { new HideoutTroopSelectionEntry(first, firstCount), new HideoutTroopSelectionEntry(second, secondCount) },
            out _, out _));
    }

    // Only the chosen counts stay, the first entries of each troop, in the order they were chosen.
    [Fact]
    public void TrimReserve_KeepsTheFirstChosenEntriesOfEachTroopInChosenOrder()
    {
        var entries = new List<TroopReserveEntry>
        {
            new(1, "recruit", 0), new(2, Hero, 0), new(3, "recruit", 0), new(4, "archer", 1), new(5, "recruit", 0),
        };

        rules.TrimReserve(entries, new[]
        {
            new HideoutTroopSelectionEntry(Hero, 1), new HideoutTroopSelectionEntry("recruit", 2),
            new HideoutTroopSelectionEntry("knight", 1),
        });

        Assert.Equal(new[] { 2, 1, 3 }, entries.Select(entry => entry.Seed));
    }

    [Fact]
    public void TryAccept_RejectsAPartyWhoseHeroCantFight()
    {
        var withoutHero = new Dictionary<string, int>(healthy) { [Hero] = 0 };

        Assert.False(rules.TryAccept(withoutHero, Hero, 4, System.Array.Empty<HideoutTroopSelectionEntry>(), out _, out _));
    }
}
