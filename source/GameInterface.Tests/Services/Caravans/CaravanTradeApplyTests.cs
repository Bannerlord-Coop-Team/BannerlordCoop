using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Caravans.Data;
using GameInterface.Services.Caravans.Handlers;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit;

namespace GameInterface.Tests.Services.Caravans;

/// <summary>Checks native trade-item reconstruction after queued registration.</summary>
[Collection(ModInformationRoleCollection.Name)]
public class CaravanTradeApplyTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    public void Apply_RequiresAllReferencedObjects(
        bool registerItem, bool hasModifier, bool registerModifier, bool expected)
    {
        GameBootStrap.Initialize();
        var manager = MBObjectManager.Instance;
        var item = ObjectHelper.SkipConstructor<ItemObject>();
        item.StringId = "caravan-test-" + Guid.NewGuid().ToString("N");
        var modifier = ObjectHelper.SkipConstructor<ItemModifier>();
        modifier.StringId = "caravan-modifier-test-" + Guid.NewGuid().ToString("N");
        var row = new TradeActionLogData
        {
            BuyPrice = 10,
            SellPrice = 12,
            ItemRosterElement = new CaravanTradeItemData
            {
                ItemObjectId = item.StringId,
                ItemModifierId = hasModifier ? modifier.StringId : null!,
                Amount = 3,
            },
        };
        using var handler = new CaravansCampaignBehaviorHandler(
            new Mock<IMessageBroker>().Object,
            new Mock<IObjectManager>().Object,
            new Mock<INetwork>().Object,
            new Mock<ISessionCaravansPlayerDataInterface>().Object,
            new Mock<ISessionInteractionsPlayerDataInterface>().Object);

        try
        {
            Assert.False(handler.UnpackTradeActionLogData(row, out _));
            if (registerItem) manager.RegisterObject(item);
            if (registerModifier) manager.RegisterObject(modifier);

            Assert.Equal(expected, handler.UnpackTradeActionLogData(row, out var result));
            if (expected)
            {
                Assert.Same(item, result.ItemRosterElement.EquipmentElement.Item);
                Assert.Same(hasModifier ? modifier : null, result.ItemRosterElement.EquipmentElement.ItemModifier);
                Assert.Equal(3, result.ItemRosterElement.Amount);
                Assert.Equal(10, result.BuyPrice);
                Assert.Equal(12, result.SellPrice);
            }
        }
        finally
        {
            if (registerModifier) manager.UnregisterObject(modifier);
            if (registerItem) manager.UnregisterObject(item);
        }
    }
}
