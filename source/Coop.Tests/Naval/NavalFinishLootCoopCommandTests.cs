#if COOP_NAVAL
using Coop.Naval.PortScreen.Commands;
using System.Collections.Generic;
using Xunit;

namespace Coop.Tests.Naval;

public class NavalFinishLootCoopCommandTests
{
    private static readonly string[] Offered =
    {
        "Ship_MobileParty_southern_pirates_371_0",
        "Ship_MobileParty_southern_pirates_371_1",
        "Ship_MobileParty_southern_pirates_371_10",
    };

    [Fact]
    public void TryParseTake_TakesNoShipWithoutAnArgument()
    {
        Assert.True(NavalFinishLootCoopCommand.TryParseTake(new string[0], out var take, out var error), error);

        Assert.False(take.All);
        Assert.Empty(take.Prefixes);
    }

    [Theory]
    [InlineData("take=none", false, new string[0])]
    [InlineData("TAKE=None", false, new string[0])]
    [InlineData("take=all", true, new string[0])]
    [InlineData("take=371_1", false, new[] { "371_1" })]
    [InlineData("take=371_0, 371_1", false, new[] { "371_0", "371_1" })]
    public void TryParseTake_ReadsNoneAllOrPrefixes(string arg, bool all, string[] prefixes)
    {
        Assert.True(NavalFinishLootCoopCommand.TryParseTake(new[] { arg }, out var take, out var error), error);

        Assert.Equal(all, take.All);
        Assert.Equal(prefixes, take.Prefixes);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("take=")]
    [InlineData("take=371_0,")]
    [InlineData("ships=all")]
    public void TryParseTake_RejectsMalformedArguments(string arg)
    {
        Assert.False(NavalFinishLootCoopCommand.TryParseTake(new[] { arg }, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryParseTake_RejectsASecondArgument()
    {
        Assert.False(NavalFinishLootCoopCommand.TryParseTake(new[] { "take=all", "extra" }, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void TryResolveTake_AllTakesEveryOfferedShip()
    {
        var take = new NavalFinishLootCoopCommand.LootTake(true, new string[0]);

        Assert.Null(NavalFinishLootCoopCommand.TryResolveTake(take, Offered, out var taken));
        Assert.Equal(Offered, taken);
    }

    [Fact]
    public void TryResolveTake_NoneTakesNothing()
    {
        Assert.Null(NavalFinishLootCoopCommand.TryResolveTake(NavalFinishLootCoopCommand.LootTake.None, Offered, out var taken));
        Assert.Empty(taken);
    }

    [Theory]
    [InlineData("Ship_MobileParty_southern_pirates_371_1", "Ship_MobileParty_southern_pirates_371_1")]
    [InlineData("ship_mobileparty_southern_pirates_371_0", "Ship_MobileParty_southern_pirates_371_0")]
    [InlineData("MobileParty_southern_pirates_371_10", "Ship_MobileParty_southern_pirates_371_10")]
    [InlineData("MobileParty_southern_pirates_371_1", "Ship_MobileParty_southern_pirates_371_1")]
    [InlineData("Ship_MobileParty_southern_pirates_371_0", "Ship_MobileParty_southern_pirates_371_0")]
    public void TryResolveTake_PicksTheOneShipAPrefixNames(string prefix, string expected)
    {
        var take = new NavalFinishLootCoopCommand.LootTake(false, new[] { prefix });

        Assert.Null(NavalFinishLootCoopCommand.TryResolveTake(take, Offered, out var taken));
        Assert.Equal(new List<string> { expected }, taken);
    }

    [Theory]
    [InlineData("Ship_MobileParty_southern_pirates_371_")]
    [InlineData("MobileParty_southern_pirates_37")]
    [InlineData("Ship_MobileParty_looters")]
    public void TryResolveTake_RefusesAPrefixThatMatchesNoneOrSeveral(string prefix)
    {
        var take = new NavalFinishLootCoopCommand.LootTake(false, new[] { prefix });

        Assert.NotNull(NavalFinishLootCoopCommand.TryResolveTake(take, Offered, out _));
    }

    [Fact]
    public void TryResolveTake_TakesARepeatedShipOnce()
    {
        var take = new NavalFinishLootCoopCommand.LootTake(false, new[] { Offered[0], Offered[0] });

        Assert.Null(NavalFinishLootCoopCommand.TryResolveTake(take, Offered, out var taken));
        Assert.Equal(new List<string> { Offered[0] }, taken);
    }
}
#endif
