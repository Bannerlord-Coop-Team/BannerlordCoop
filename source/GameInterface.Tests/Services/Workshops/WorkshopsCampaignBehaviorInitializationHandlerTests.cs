using Common.Util;
using GameInterface.Services.Workshops.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit;

namespace GameInterface.Tests.Services.Workshops;

public class WorkshopsCampaignBehaviorInitializationHandlerTests
{
    [Fact]
    public void AddMissingPlayerWorkshopRosters_OwnerMatchesPlayer_AddsEmptyRosterForWorkshopSettlement()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = CreateWorkshop(playerHero, settlement);
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>();

        WorkshopsCampaignBehaviorInitializationHandler.AddMissingPlayerWorkshopRosters(
            warehouseRosters,
            playerHero,
            new[] { workshop });

        ItemRoster roster = Assert.Contains(settlement, warehouseRosters);
        Assert.Empty(roster);
    }

    [Fact]
    public void AddMissingPlayerWorkshopRosters_OwnerDoesNotMatchPlayer_DoesNotAddRoster()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Hero otherHero = ObjectHelper.SkipConstructor<Hero>();
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = CreateWorkshop(otherHero, settlement);
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>();

        WorkshopsCampaignBehaviorInitializationHandler.AddMissingPlayerWorkshopRosters(
            warehouseRosters,
            playerHero,
            new[] { workshop });

        Assert.Empty(warehouseRosters);
    }

    [Fact]
    public void AddMissingPlayerWorkshopRosters_SavedRoster_PreservesRoster()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = CreateWorkshop(playerHero, settlement);
        var savedRoster = new ItemRoster();
        var warehouseRosters = new Dictionary<Settlement, ItemRoster>
        {
            [settlement] = savedRoster,
        };

        WorkshopsCampaignBehaviorInitializationHandler.AddMissingPlayerWorkshopRosters(
            warehouseRosters,
            playerHero,
            new[] { workshop });

        Assert.Same(savedRoster, Assert.Contains(settlement, warehouseRosters));
    }

    [Fact]
    public void EnsurePlayerWorkshopData_OwnerMatchesPlayer_AddsWorkshopData()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = CreateWorkshop(playerHero, settlement);
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();

        WorkshopsCampaignBehaviorInitializationHandler.EnsurePlayerWorkshopData(
            workshopsBehavior,
            playerHero,
            new[] { workshop });

        WorkshopsCampaignBehavior.WorkshopData workshopData =
            Assert.Single(workshopsBehavior._workshopData.Where(data => data != null))!;
        Assert.Same(workshop, workshopData.Workshop);
    }

    [Fact]
    public void EnsurePlayerWorkshopData_ExistingWorkshopData_DoesNotDuplicateEntry()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Settlement settlement = ObjectHelper.SkipConstructor<Settlement>();
        Workshop workshop = CreateWorkshop(playerHero, settlement);
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        workshopsBehavior._workshopData =
            new[] { new WorkshopsCampaignBehavior.WorkshopData(workshop) };

        WorkshopsCampaignBehaviorInitializationHandler.EnsurePlayerWorkshopData(
            workshopsBehavior,
            playerHero,
            new[] { workshop });

        WorkshopsCampaignBehavior.WorkshopData workshopData =
            Assert.Single(workshopsBehavior._workshopData)!;
        Assert.Same(workshop, workshopData.Workshop);
    }

    [Fact]
    public void EnsurePlayerWorkshopData_FullStorage_ExpandsForPlayerWorkshop()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Hero otherHero = ObjectHelper.SkipConstructor<Hero>();
        Workshop existingWorkshop = CreateWorkshop(otherHero, ObjectHelper.SkipConstructor<Settlement>());
        Workshop playerWorkshop = CreateWorkshop(playerHero, ObjectHelper.SkipConstructor<Settlement>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();
        workshopsBehavior._workshopData =
            new[] { new WorkshopsCampaignBehavior.WorkshopData(existingWorkshop) };

        WorkshopsCampaignBehaviorInitializationHandler.EnsurePlayerWorkshopData(
            workshopsBehavior,
            playerHero,
            new[] { existingWorkshop, playerWorkshop });

        Assert.Equal(2, workshopsBehavior._workshopData.Length);
        Assert.Contains(workshopsBehavior._workshopData, data => data?.Workshop == existingWorkshop);
        Assert.Contains(workshopsBehavior._workshopData, data => data?.Workshop == playerWorkshop);
    }

    [Fact]
    public void EnsurePlayerWorkshopData_OwnerDoesNotMatchPlayer_DoesNotAddEntry()
    {
        Hero playerHero = ObjectHelper.SkipConstructor<Hero>();
        Hero otherHero = ObjectHelper.SkipConstructor<Hero>();
        Workshop workshop = CreateWorkshop(otherHero, ObjectHelper.SkipConstructor<Settlement>());
        WorkshopsCampaignBehavior workshopsBehavior = CreateWorkshopsBehavior();

        WorkshopsCampaignBehaviorInitializationHandler.EnsurePlayerWorkshopData(
            workshopsBehavior,
            playerHero,
            new[] { workshop });

        Assert.Empty(workshopsBehavior._workshopData);
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

    private static Workshop CreateWorkshop(Hero owner, Settlement settlement)
    {
        Workshop workshop = ObjectHelper.SkipConstructor<Workshop>();
        typeof(Workshop).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(workshop, owner);
        typeof(Workshop).GetField("_settlement", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(workshop, settlement);
        return workshop;
    }

    private static WorkshopsCampaignBehavior CreateWorkshopsBehavior()
    {
        WorkshopsCampaignBehavior workshopsBehavior =
            ObjectHelper.SkipConstructor<WorkshopsCampaignBehavior>();
        workshopsBehavior._workshopData = Array.Empty<WorkshopsCampaignBehavior.WorkshopData>();
        return workshopsBehavior;
    }
}
