using Common.Messaging;
using Common.Network;
using GameInterface.Services.Inventory.Handlers;
using GameInterface.Services.Inventory.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Interfaces;
using Moq;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Services.Inventory.Handlers;

public class TradeHandlerTests
{
    [Fact]
    public void ReconcilePurchases_NoShortfall_WhenExactModifierStockCoversPurchase()
    {
        var shield = new ItemObject("old_horsemans_kite_shield");
        var thick = new ItemModifier();

        var merchantRoster = new ItemRoster();
        merchantRoster.AddToCounts(new EquipmentElement(shield, thick), 1);
        merchantRoster.AddToCounts(new EquipmentElement(shield, null), 4);

        var merchantRosterData = merchantRoster.ToArray();
        var partyRosterData = new[]
        {
            new ItemRosterElement(new EquipmentElement(shield, null), 2)
        };

        var boughtItems = new List<(ItemRosterElement, int)>
        {
            (new ItemRosterElement(new EquipmentElement(shield, null), 2), 534)
        };

        int result = TradeHandler.ReconcilePurchases(merchantRoster, ref merchantRosterData, ref partyRosterData, boughtItems, totalAmount: 534);

        Assert.Equal(534, result);
        Assert.Equal(2, partyRosterData[0].Amount);
    }

    [Fact]
    public void ReconcilePurchases_ProratesRefund_OnGenuineShortfall()
    {
        var shield = new ItemObject("old_horsemans_kite_shield");

        var merchantRoster = new ItemRoster();
        merchantRoster.AddToCounts(new EquipmentElement(shield, null), 1);

        var merchantRosterData = merchantRoster.ToArray();
        var partyRosterData = new[]
        {
            new ItemRosterElement(new EquipmentElement(shield, null), 4)
        };

        var boughtItems = new List<(ItemRosterElement, int)>
        {
            (new ItemRosterElement(new EquipmentElement(shield, null), 4), 400)
        };

        int result = TradeHandler.ReconcilePurchases(merchantRoster, ref merchantRosterData, ref partyRosterData, boughtItems, totalAmount: 400);

        // 1 of 4 available: 3 short, refunded at 100 each, so only 100 is paid and 1 is kept.
        Assert.Equal(100, result);
        Assert.Equal(1, partyRosterData[0].Amount);
        Assert.Equal(4, merchantRosterData.Single(e => e.EquipmentElement.Item == shield).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconcilePurchases_RestoresShortfall_WhenMerchantEntryIsMissing(bool withTombstone)
    {
        var shield = new ItemObject("old_horsemans_kite_shield");

        var merchantRoster = new ItemRoster();
        merchantRoster.AddToCounts(new EquipmentElement(shield, null), 3);

        // The client bought every shield it saw, so its merchant proposal has no shield entry left
        var merchantRosterData = withTombstone ? new[] { default(ItemRosterElement) } : System.Array.Empty<ItemRosterElement>();
        var partyRosterData = new[]
        {
            new ItemRosterElement(new EquipmentElement(shield, null), 5)
        };

        var boughtItems = new List<(ItemRosterElement, int)>
        {
            (new ItemRosterElement(new EquipmentElement(shield, null), 5), 100)
        };

        int result = TradeHandler.ReconcilePurchases(merchantRoster, ref merchantRosterData, ref partyRosterData, boughtItems, totalAmount: 100);

        // 3 of 5 available: 2 go back to the merchant and 40 is refunded.
        Assert.Equal(60, result);
        Assert.Equal(3, partyRosterData[0].Amount);
        var restored = Assert.Single(merchantRosterData, e => e.EquipmentElement.Item == shield);
        Assert.Equal(2, restored.Amount);
        Assert.Equal(withTombstone ? 2 : 1, merchantRosterData.Length);
    }

    [Fact]
    public void ReconcilePurchases_NoRefund_WhenPartyEntryIsMissing()
    {
        var shield = new ItemObject("old_horsemans_kite_shield");

        var merchantRoster = new ItemRoster();
        merchantRoster.AddToCounts(new EquipmentElement(shield, null), 3);

        // The bought shields left the party roster (equipped during the trade), so none can be taken back
        var merchantRosterData = System.Array.Empty<ItemRosterElement>();
        var partyRosterData = new[] { default(ItemRosterElement) };

        var boughtItems = new List<(ItemRosterElement, int)>
        {
            (new ItemRosterElement(new EquipmentElement(shield, null), 5), 100)
        };

        int result = TradeHandler.ReconcilePurchases(merchantRoster, ref merchantRosterData, ref partyRosterData, boughtItems, totalAmount: 100);

        Assert.Equal(100, result);
        Assert.Empty(merchantRosterData);
        Assert.Equal(0, partyRosterData[0].Amount);
    }

    [Fact]
    public void ReconcilePurchases_RefundsOnlyWhatThePartyStillHolds()
    {
        var shield = new ItemObject("old_horsemans_kite_shield");

        var merchantRoster = new ItemRoster();
        merchantRoster.AddToCounts(new EquipmentElement(shield, null), 3);

        var merchantRosterData = System.Array.Empty<ItemRosterElement>();
        var partyRosterData = new[]
        {
            new ItemRosterElement(new EquipmentElement(shield, null), 1)
        };

        var boughtItems = new List<(ItemRosterElement, int)>
        {
            (new ItemRosterElement(new EquipmentElement(shield, null), 5), 100)
        };

        int result = TradeHandler.ReconcilePurchases(merchantRoster, ref merchantRosterData, ref partyRosterData, boughtItems, totalAmount: 100);

        // 2 short but the party only holds 1: that 1 goes back and 20 is refunded.
        Assert.Equal(80, result);
        Assert.Equal(0, partyRosterData[0].Amount);
        Assert.Equal(1, Assert.Single(merchantRosterData).Amount);
    }

    [Fact]
    public void TryResolveLeftRemainder_SkipsEmptyTombstoneSlots()
    {
        // Vanilla leaves default (null item, zero amount) slots behind when a kind
        // is fully taken. The strict mock proves they are skipped before any
        // registry lookup: a lookup with null would throw.
        var grain = new ItemObject("grain");
        string grainId = "ItemObject_grain";
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.Setup(m => m.TryGetId(It.IsNotNull<object>(), out grainId)).Returns(true);
        var handler = CreateHandler(objectManager.Object);

        var data = new[]
        {
            new ItemRosterElement(new EquipmentElement(grain, null), 2),
            default(ItemRosterElement),
        };

        Assert.True(handler.TryResolveLeftRemainder(data, out var remainder, out var error));
        Assert.Null(error);
        var single = Assert.Single(remainder);
        Assert.Equal(grainId, single.itemId);
        Assert.Equal(2, single.amount);
    }

    [Fact]
    public void TryResolveLeftRemainder_RejectsNullItemWithNegativeAmount()
    {
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        var handler = CreateHandler(objectManager.Object);

        var data = new[]
        {
            new ItemRosterElement(new EquipmentElement((ItemObject)null, (ItemModifier)null), -1),
        };

        Assert.False(handler.TryResolveLeftRemainder(data, out _, out var error));
        Assert.Contains("[0/1]", error);
        Assert.Contains("Amount=-1", error);
        Assert.Contains("ItemNull=True", error);
    }

    [Fact]
    public void TryResolveLeftRemainder_RejectsNullItemWithPositiveAmount()
    {
        string nullId = null;
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.Setup(m => m.TryGetId(null, out nullId)).Returns(false);
        var handler = CreateHandler(objectManager.Object);

        var data = new[]
        {
            new ItemRosterElement(new EquipmentElement((ItemObject)null, (ItemModifier)null), 3),
        };

        Assert.False(handler.TryResolveLeftRemainder(data, out _, out var error));
        Assert.Contains("[0/1]", error);
        Assert.Contains("Amount=3", error);
        Assert.Contains("ItemNull=True", error);
    }

    [Fact]
    public void TryResolveLeftRemainder_RejectsUnregisteredItem()
    {
        string unknownId = null;
        var objectManager = new Mock<IObjectManager>(MockBehavior.Loose);
        objectManager.Setup(m => m.TryGetId(It.IsNotNull<object>(), out unknownId)).Returns(false);
        var handler = CreateHandler(objectManager.Object);

        var iron = new ItemObject("iron");
        var data = new[]
        {
            new ItemRosterElement(new EquipmentElement(iron, null), 1),
        };

        Assert.False(handler.TryResolveLeftRemainder(data, out _, out var error));
        Assert.Contains("[0/1]", error);
        Assert.Contains("StringId=iron", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void ResolveLeftLootIds_ResolvesOccupiedEntriesWithoutLookingUpSpareCapacity(int itemCount)
    {
        var roster = new ItemRoster();
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        for (int i = 0; i < itemCount; i++)
        {
            var item = new ItemObject($"discard_item_{i}");
            string itemId = $"ItemObject_discard_item_{i}";
            roster.AddToCounts(item, i + 1);
            objectManager.Setup(m => m.TryGetId(item, out itemId)).Returns(true);
        }
        Assert.True(roster._data.Length > roster.Count);
        using var handler = CreateHandler(objectManager.Object);

        var resolved = handler.ResolveLeftLootIds(roster);

        Assert.Equal(itemCount, resolved.Length);
        for (int i = 0; i < itemCount; i++)
        {
            Assert.Equal($"ItemObject_discard_item_{i}", resolved[i].Item1.ItemObjectData.ItemObjectId);
            Assert.Equal(i + 1, resolved[i].Item1.Amount);
            Assert.Equal(i + 1, resolved[i].Item2);
        }
        objectManager.VerifyAll();
    }

    [Fact]
    public void ResolveLeftLootIds_AfterRemoval_PreservesRemainingModifierAndAmount()
    {
        var removed = new ItemObject("grain");
        var shield = new ItemObject("old_horsemans_kite_shield");
        var thick = new ItemModifier();
        var roster = new ItemRoster();
        roster.AddToCounts(removed, 1);
        roster.AddToCounts(new EquipmentElement(shield, thick), 3);
        roster.AddToCounts(removed, -1);
        string shieldId = "ItemObject_old_horsemans_kite_shield";
        string thickId = "ItemModifier_thick";
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.Setup(m => m.TryGetId(shield, out shieldId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(thick, out thickId)).Returns(true);
        using var handler = CreateHandler(objectManager.Object);

        var resolved = Assert.Single(handler.ResolveLeftLootIds(roster));

        Assert.Equal(shieldId, resolved.Item1.ItemObjectData.ItemObjectId);
        Assert.Equal(thickId, resolved.Item1.ItemObjectData.ItemModifierId);
        Assert.False(resolved.Item1.ItemObjectData.ItemModifierNull);
        Assert.Equal(3, resolved.Item1.Amount);
        Assert.Equal(3, resolved.Item2);
        objectManager.VerifyAll();
    }

    [Fact]
    public void ResolveLeftLootIds_UnregisteredOccupiedItem_StillAttemptsResolution()
    {
        var item = new ItemObject("iron");
        var roster = new ItemRoster();
        roster.AddToCounts(item, 1);
        string itemId = null;
        var objectManager = new Mock<IObjectManager>(MockBehavior.Strict);
        objectManager.Setup(m => m.TryGetId(item, out itemId)).Returns(false);
        using var handler = CreateHandler(objectManager.Object);

        Assert.Empty(handler.ResolveLeftLootIds(roster));

        objectManager.Verify(m => m.TryGetId(item, out itemId), Times.Once);
    }

    private static TradeHandler CreateHandler(IObjectManager objectManager)
    {
        return new TradeHandler(
            Mock.Of<IInventoryLogicInterface>(),
            Mock.Of<IMessageBroker>(),
            objectManager,
            Mock.Of<INetwork>(),
            Mock.Of<IVillageHostileActionInterface>());
    }
}
