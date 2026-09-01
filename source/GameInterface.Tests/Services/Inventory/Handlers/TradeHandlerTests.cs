using GameInterface.Services.Inventory.Handlers;
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
}
