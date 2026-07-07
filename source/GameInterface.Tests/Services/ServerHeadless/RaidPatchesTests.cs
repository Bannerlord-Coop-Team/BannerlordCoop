using ServerHeadless.Bootstrap.Patches;
using System.Collections.Generic;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.ServerHeadless;

public class RaidPatchesTests
{
    [Fact]
    public void BuildCommonLootItemScores_PreservesResolvedItemsAndSkipsMissingItems()
    {
        var hides = new ItemObject("hides");
        var grain = new ItemObject("grain");
        hides.Value = 24;
        grain.Value = 9;

        var resolvedItems = new Dictionary<string, ItemObject>
        {
            ["hides"] = hides,
            ["grain"] = grain,
        };
        var missingItemIds = new List<string>();

        var loot = RaidPatches.BuildCommonLootItemScores(
            itemId => resolvedItems.TryGetValue(itemId, out var item) ? item : null!,
            missingItemIds.Add);

        Assert.Equal(2, loot.Count);
        Assert.Contains(loot, item => item.Item1 == hides && item.Item2 == 4f);
        Assert.Contains(loot, item => item.Item1 == grain && item.Item2 == 10f);
        Assert.DoesNotContain("hides", missingItemIds);
        Assert.DoesNotContain("grain", missingItemIds);
        Assert.Contains("hardwood", missingItemIds);
        Assert.Contains("pottery", missingItemIds);
    }

    [Fact]
    public void BuildCommonLootItemScores_ReturnsEmptyLootWhenNoItemsResolve()
    {
        var missingItemIds = new List<string>();

        var loot = RaidPatches.BuildCommonLootItemScores(_ => null!, missingItemIds.Add);

        Assert.Empty(loot);
        Assert.Contains("hides", missingItemIds);
        Assert.Contains("pottery", missingItemIds);
    }
}