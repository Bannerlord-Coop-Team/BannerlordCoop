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

        int result = TradeHandler.ReconcilePurchases(merchantRoster, merchantRosterData, partyRosterData, boughtItems, totalAmount: 534);

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

        int result = TradeHandler.ReconcilePurchases(merchantRoster, merchantRosterData, partyRosterData, boughtItems, totalAmount: 400);

        // 1 of 4 available: 3 short, refunded at 100 each, so only 100 is paid and 1 is kept.
        Assert.Equal(100, result);
        Assert.Equal(1, partyRosterData[0].Amount);
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
