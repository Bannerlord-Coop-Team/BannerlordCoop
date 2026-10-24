using GameInterface.Services.Inventory.Handlers;
using GameInterface.Services.Inventory.Interfaces;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Inventory;

/// <summary>Food changes must survive inventory cancellation and stale confirmation.</summary>
public class InventoryFoodUpkeepTests
{
    [Theory]
    [InlineData(5, -2, 3)]
    [InlineData(1, -1, 0)]
    public void ReceivedFoodConsumption_UpdatesCancelBackup(int initial, int change, int expected)
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, initial);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);

        Assert.True(inventory.TryApplyInventoryUpdate(logic, logic._rosters[1], grain, change));

        Assert.Equal(expected, Count(logic._rosters[1], grain));
        Assert.Equal(expected, Count(logic._rostersBackup[1], grain));
        Assert.Equal(expected, inventory.GetInventoryBaseline(logic).Sum(item => item.Amount));
    }

    [Fact]
    public void ReceivedFoodConsumption_PreservesPendingDiscardAndUpdatesCancelBackup()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 10);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);
        logic._rosters[1].AddToCounts(grain, -3);
        logic._rosters[0].AddToCounts(grain, 3);

        inventory.TryApplyInventoryUpdate(logic, logic._rosters[1], grain, -2);
        inventory.TryApplyInventoryUpdate(logic, logic._rosters[1], grain, -1);

        Assert.Equal(4, Count(logic._rosters[1], grain));
        Assert.Equal(7, Count(logic._rostersBackup[1], grain));
        Assert.Equal(3, Count(logic._rosters[0], grain));
    }

    [Fact]
    public void BackupRefreshForSlaughter_DoesNotReplaceAuthoritativeBaseline()
    {
        var sheep = new EquipmentElement(new ItemObject("sheep"));
        var meat = new EquipmentElement(new ItemObject("meat"));
        var logic = CreateLogic(sheep, 2);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);
        logic._rosters[1].AddToCounts(sheep, -1);
        logic._rosters[1].AddToCounts(meat, 2);
        logic._rostersBackup[1] = new ItemRoster(logic._rosters[1]);

        Assert.Equal(2, inventory.GetInventoryBaseline(logic).Single().Amount);
        inventory.RestoreInventoryBackup(logic);
        Assert.Equal(2, Count(logic._rostersBackup[1], sheep));
        Assert.Equal(0, Count(logic._rostersBackup[1], meat));
    }

    [Fact]
    public void ConflictBackupRestore_DiscardsBothStagedSidesAndRetainsReceivedStock()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 5);
        logic._rosters[0].AddToCounts(grain, 10);
        logic._rostersBackup[0] = new ItemRoster(logic._rosters[0]);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);
        logic._rosters[1].AddToCounts(grain, -5);
        logic._rosters[0].AddToCounts(grain, 5);
        // Simulate the roster backup refresh performed by native slaughter.
        logic._rostersBackup[0] = new ItemRoster(logic._rosters[0]);
        logic._rostersBackup[1] = new ItemRoster(logic._rosters[1]);
        Assert.True(inventory.TryApplyInventoryUpdate(logic, logic._rosters[0], grain, 2));

        inventory.RestoreInventoryBackup(logic);

        Assert.Equal(5, Count(logic._rostersBackup[1], grain));
        Assert.Equal(12, Count(logic._rostersBackup[0], grain));
        logic._rostersBackup[1].Clear();
        Assert.Equal(5, inventory.GetInventoryBaseline(logic).Single().Amount);
    }

    [Fact]
    public void StalePlayerRoster_RebasesSubmittedChangesOnAuthoritativeFood()
    {
        var grain = new ItemObject("grain");
        var authoritative = new ItemRoster();
        authoritative.AddToCounts(grain, 8);
        var initial = new ItemRosterElement[] { new(grain, 10) };
        var submitted = new ItemRosterElement[] { new(grain, 7) };

        var rebased = TradeHandler.RebaseInventoryData(authoritative, initial, submitted);
        Assert.Equal(5, rebased.Single().Amount);
    }

    [Fact]
    public void EmptyAuthoritativeRoster_AppliesThePurchase()
    {
        var grain = new ItemObject("grain");
        var submitted = new ItemRosterElement[] { new(grain, 1) };

        var rebased = TradeHandler.RebaseInventoryData(new ItemRoster(), Array.Empty<ItemRosterElement>(), submitted);
        Assert.Equal(1, rebased.Single().Amount);
    }

    [Fact]
    public void StalePlayerRoster_ClampsDiffThatWouldGoBelowZero()
    {
        var grain = new ItemObject("grain");
        var authoritative = new ItemRoster();
        authoritative.AddToCounts(grain, 2);
        var initial = new ItemRosterElement[] { new(grain, 10) };
        var submitted = new ItemRosterElement[] { new(grain, 0) };

        var rebased = TradeHandler.RebaseInventoryData(authoritative, initial, submitted);
        Assert.Empty(rebased);
    }

    [Fact]
    public void ServerRosterSnapshot_AppliesElementDeltasWithoutClearingTheRoster()
    {
        var grain = new ItemObject("grain");
        var sheep = new ItemObject("sheep");
        var roster = new ItemRoster();
        roster.AddToCounts(grain, 10);
        var logic = new InventoryLogicInterface(null, null);

        logic.UpdateRosterWithData(roster, new[]
        {
            new ItemRosterElement(grain, 7),
            new ItemRosterElement(sheep, 2),
        });

        Assert.Equal(7, Count(roster, new EquipmentElement(grain)));
        Assert.Equal(2, Count(roster, new EquipmentElement(sheep)));
    }

    [Fact]
    public void EmptyInventoryChangedBeforeDone_PreservesLootAndServerItems()
    {
        var grain = new ItemObject("grain");
        var loot = new ItemObject("shield");
        var server = new ItemRoster();
        server.AddToCounts(grain, 3);
        var submitted = new[] { new ItemRosterElement(loot, 1) };

        var rebased = TradeHandler.RebaseInventoryData(server, null, submitted);

        Assert.Equal(3, rebased.Single(item => item.EquipmentElement.Item == grain).Amount);
        Assert.Equal(1, rebased.Single(item => item.EquipmentElement.Item == loot).Amount);
        Assert.Equal(3, TradeHandler.RebaseInventoryData(server, null, null).Single().Amount);
    }

    [Fact]
    public void EmptySubmittedWireRoster_RemovesOnlyTheInitialItems()
    {
        var grain = new ItemObject("grain");
        var loot = new ItemObject("shield");
        var server = new ItemRoster();
        server.AddToCounts(grain, 3);
        server.AddToCounts(loot, 1);

        var rebased = TradeHandler.RebaseInventoryData(server,
            new[] { new ItemRosterElement(grain, 3) }, null);

        Assert.Same(loot, rebased.Single().EquipmentElement.Item);
    }

    [Fact]
    public void MarketUpdate_PreservesPendingPurchaseAndDoneBaseline()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 5);
        logic._rosters[0].AddToCounts(grain, 10);
        logic._rostersBackup[0] = new ItemRoster(logic._rosters[0]);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);
        logic._rosters[0].AddToCounts(grain, -3);
        logic._rosters[1].AddToCounts(grain, 3);

        Assert.True(inventory.TryApplyInventoryUpdate(logic, logic._rosters[0], grain, -2));
        Assert.Equal(5, Count(logic._rosters[0], grain));
        Assert.Equal(8, Count(logic._rostersBackup[0], grain));
        inventory.RestoreInventoryBackup(logic);
        Assert.Equal(8, Count(logic._rostersBackup[0], grain));
        Assert.Equal(5, Count(logic._rostersBackup[1], grain));
    }

    [Fact]
    public void UnrelatedRoster_IsNotAppliedByInventory()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 5);
        var inventory = new InventoryScreenRosterRefresher();
        inventory.CaptureInventoryBaseline(logic);

        Assert.False(inventory.TryApplyInventoryUpdate(logic, new ItemRoster(), grain, -1));
        Assert.Equal(5, Count(logic._rosters[1], grain));
    }

    private static InventoryLogic CreateLogic(EquipmentElement item, int amount)
    {
        var logic = new InventoryLogic(null, null, null);
        logic._rosters[0] = new ItemRoster();
        logic._rosters[1] = new ItemRoster();
        logic._rosters[1].AddToCounts(item, amount);
        logic._rostersBackup[0] = new ItemRoster();
        logic._rostersBackup[1] = new ItemRoster(logic._rosters[1]);
        return logic;
    }

    private static int Count(ItemRoster roster, EquipmentElement item)
    {
        int index = roster.FindIndexOfElement(item);
        return index < 0 ? 0 : roster.GetElementNumber(index);
    }
}
