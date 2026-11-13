using GameInterface.Services.Party;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Party;

/// <summary>
/// The limits the server replay puts on a single command, so a stale screen can't apply more than the party has.
/// </summary>
public class PartyScreenCommandProcessorTests
{
    [Theory]
    // requested, troops, xp, xpCost, gold, goldCost, items, expected number, expected reason
    [InlineData(3, 5, 900, 300, 1000, 100, null, 3, null)]
    [InlineData(3, 2, 900, 300, 1000, 100, null, 2, "troops")]
    [InlineData(3, 5, 600, 300, 1000, 100, null, 2, "experience")]
    [InlineData(3, 5, 900, 300, 200, 100, null, 2, "gold")]
    [InlineData(3, 5, 900, 300, -50, 100, null, 0, "gold")]
    [InlineData(3, 5, 900, 300, 1000, 100, 2, 2, "Horse")]
    [InlineData(3, 5, 900, 0, 0, 0, null, 3, null)]
    public void LimitUpgrade_CapsByWhatRunsOutAndNamesIt(
        int requested, int troops, int xp, int xpCost, int gold, int goldCost, int? items, int expectedNumber, string expectedShortOf)
    {
        var (number, shortOf) = PartyScreenCommandProcessor.LimitUpgrade(requested, troops, xp, xpCost, gold, goldCost, items, "Horse");

        Assert.Equal(expectedNumber, number);
        Assert.Equal(expectedShortOf, shortOf);
    }

    [Theory]
    // The right side keeps enough xp for the troops it still has.
    [InlineData(true, 2, 5, 1000, 300, 100)]
    [InlineData(true, 5, 5, 1000, 300, 1000)]
    [InlineData(true, 1, 5, 500, 300, 0)]
    // The left side gives up to what the moved troops can hold.
    [InlineData(false, 2, 5, 1000, 300, 600)]
    [InlineData(false, 2, 5, 100, 300, 100)]
    public void TransferredXp_SplitsXpBetweenTheMovedAndTheKeptTroops(
        bool fromRight, int number, int available, int availableXp, int xpPerTroop, int expected)
    {
        Assert.Equal(expected, PartyScreenCommandProcessor.TransferredXp(fromRight, number, available, availableXp, xpPerTroop));
    }

    [Theory]
    // requested, number moved, stack size, stack wounded, expected
    [InlineData(1, 2, 5, 3, 1)]
    // Healing left fewer wounded than the player moved.
    [InlineData(3, 3, 5, 1, 1)]
    // Injuries left too few healthy troops for an all healthy move.
    [InlineData(0, 3, 5, 4, 2)]
    public void ClampWounded_FitsTheWoundedToWhatTheStackHas(int requested, int number, int available, int availableWounded, int expected)
    {
        Assert.Equal(expected, PartyScreenCommandProcessor.ClampWounded(requested, number, available, availableWounded));
    }

    [Fact]
    public void RemoveItems_UsesUnlockedItemsBeforeLockedOnes()
    {
        var horses = new ItemCategory("horse");
        var lockedCheap = CreateItem("cheap_horse", horses, 100);
        var unlockedDear = CreateItem("dear_horse", horses, 500);
        var roster = new ItemRoster();
        roster.AddToCounts(lockedCheap, 2);
        roster.AddToCounts(unlockedDear, 2);

        PartyScreenCommandProcessor.RemoveItems(roster, horses, 3, new[] { "cheap_horse" });

        Assert.Equal(1, roster.GetItemNumber(lockedCheap));
        Assert.Equal(0, roster.GetItemNumber(unlockedDear));
    }

    [Fact]
    public void RemoveItems_WithoutLocks_UsesTheCheapestFirst()
    {
        var horses = new ItemCategory("horse");
        var cheap = CreateItem("cheap_horse", horses, 100);
        var dear = CreateItem("dear_horse", horses, 500);
        var roster = new ItemRoster();
        roster.AddToCounts(dear, 2);
        roster.AddToCounts(cheap, 2);

        PartyScreenCommandProcessor.RemoveItems(roster, horses, 1, Array.Empty<string>());

        Assert.Equal(1, roster.GetItemNumber(cheap));
        Assert.Equal(2, roster.GetItemNumber(dear));
    }

    private static ItemObject CreateItem(string id, ItemCategory category, int value)
    {
        var item = new ItemObject(id);
        item.ItemCategory = category;
        item.Value = value;
        return item;
    }
}
