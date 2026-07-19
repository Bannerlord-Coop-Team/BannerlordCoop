using Common.Util;
using GameInterface.Services.Workshops.Handlers;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit;

namespace GameInterface.Tests.Services.Workshops;

public class WorkshopsCampaignBehaviorInitializationHandlerTests
{
    [Fact]
    public void AddMissingOwnedWorkshopRosters_NoSavedRoster_AddsEmptyRosterForWorkshopSettlement()
    {
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = ObjectHelper.SkipConstructor<Workshop>();
        typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(workshop, settlement);
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>();

        WorkshopsCampaignBehaviorInitializationHandler.AddMissingOwnedWorkshopRosters(
            warehouseRosters,
            new[] { workshop });

        ItemRoster roster = Assert.Contains(settlement, warehouseRosters);
        Assert.Empty(roster);
    }

    [Fact]
    public void CreateWarehouseRosterSlots_NoSavedRosters_UsesMinimumCapacity()
    {
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>();

        KeyValuePair<Settlement, ItemRoster>[] result =
            WorkshopsCampaignBehaviorInitializationHandler.CreateWarehouseRosterSlots(warehouseRosters, 4);

        Assert.Equal(4, result.Length);
        Assert.All(result, slot =>
        {
            Assert.Null(slot.Key);
            Assert.Null(slot.Value);
        });
    }

    [Fact]
    public void CreateWarehouseRosterSlots_SavedRosters_PreservesEntriesAndUnusedCapacity()
    {
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        var itemRoster = new ItemRoster();
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>
        {
            [settlement] = itemRoster,
        };

        KeyValuePair<Settlement, ItemRoster>[] result =
            WorkshopsCampaignBehaviorInitializationHandler.CreateWarehouseRosterSlots(warehouseRosters, 3);

        Assert.Equal(3, result.Length);
        Assert.Same(settlement, result[0].Key);
        Assert.Same(itemRoster, result[0].Value);
        Assert.Null(result[1].Key);
        Assert.Null(result[1].Value);
    }

    [Fact]
    public void CreateWarehouseRosterSlots_MoreRostersThanMinimum_PreservesEveryEntry()
    {
        Settlement firstSettlement = ObjectHelper.SkipConstructor<Settlement>();
        Settlement secondSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var firstRoster = new ItemRoster();
        var secondRoster = new ItemRoster();
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>
        {
            [firstSettlement] = firstRoster,
            [secondSettlement] = secondRoster,
        };

        KeyValuePair<Settlement, ItemRoster>[] result =
            WorkshopsCampaignBehaviorInitializationHandler.CreateWarehouseRosterSlots(warehouseRosters, 1);

        Assert.Equal(2, result.Length);
        Assert.Contains(result, slot => slot.Key == firstSettlement && slot.Value == firstRoster);
        Assert.Contains(result, slot => slot.Key == secondSettlement && slot.Value == secondRoster);
    }
}
