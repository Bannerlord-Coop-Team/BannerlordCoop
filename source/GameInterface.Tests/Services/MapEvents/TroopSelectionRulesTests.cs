using GameInterface.Services.Hideouts;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Moq;
using System.Collections.Generic;
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
            out var accepted));

        Assert.Equal(new[] { new HideoutTroopSelectionEntry(Hero, 1), new HideoutTroopSelectionEntry("recruit", 3) },
            accepted);
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
            out _));
    }

    [Fact]
    public void TryAccept_RejectsAPartyWhoseHeroCantFight()
    {
        var withoutHero = new Dictionary<string, int>(healthy) { [Hero] = 0 };

        Assert.False(rules.TryAccept(withoutHero, Hero, 4, System.Array.Empty<HideoutTroopSelectionEntry>(), out _));
    }
}
