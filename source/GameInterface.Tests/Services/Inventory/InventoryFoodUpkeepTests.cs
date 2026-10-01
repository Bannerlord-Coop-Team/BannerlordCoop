using GameInterface.Services.Inventory.Handlers;
using GameInterface.Services.Inventory.Interfaces;
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
        var inventory = new InventoryLogicInterface(null, null);
        inventory.CaptureInventoryBaseline(logic);

        Assert.True(inventory.TryApplyInventoryUpdate(logic, logic._rosters[1], grain, change));

        Assert.Equal(expected, Count(logic._rosters[1], grain));
        Assert.Equal(expected, Count(logic._rostersBackup[1], grain));
        Assert.True(TradeHandler.MatchesInventoryBaseline(logic._rosters[1], inventory.GetInventoryBaseline(logic)));
    }

    [Fact]
    public void ReceivedFoodConsumption_PreservesPendingDiscardAndUpdatesCancelBackup()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 10);
        var inventory = new InventoryLogicInterface(null, null);
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
        var inventory = new InventoryLogicInterface(null, null);
        inventory.CaptureInventoryBaseline(logic);
        var server = new ItemRoster(logic._rosters[1]);
        logic._rosters[1].AddToCounts(sheep, -1);
        logic._rosters[1].AddToCounts(meat, 2);
        logic._rostersBackup[1] = new ItemRoster(logic._rosters[1]);

        Assert.True(TradeHandler.MatchesInventoryBaseline(server, inventory.GetInventoryBaseline(logic)));
        Assert.False(TradeHandler.MatchesInventoryBaseline(server, logic._rostersBackup[1].ToArray()));
    }

    [Fact]
    public void ServerConsumptionAfterSubmission_RejectsStaleConfirmation()
    {
        var grain = new ItemObject("grain");
        var server = new ItemRoster();
        server.AddToCounts(grain, 5);
        var baseline = server.ToArray();
        server.AddToCounts(grain, -2);

        Assert.False(TradeHandler.MatchesInventoryBaseline(server, baseline));
        Assert.Equal(3, server.GetElementNumber(0));
    }

    [Fact]
    public void BaselineComparison_UsesModifierIdentityAndHandlesEmptyWireArray()
    {
        var item = new ItemObject("shield");
        var modifier = new ItemModifier();
        var server = new ItemRoster();
        server.AddToCounts(new EquipmentElement(item, modifier), 1);
        var differentModifier = new[] { new ItemRosterElement(item, 1) };

        Assert.False(TradeHandler.MatchesInventoryBaseline(server, differentModifier));
        Assert.True(TradeHandler.MatchesInventoryBaseline(server, server.ToArray()));
        Assert.True(TradeHandler.MatchesInventoryBaseline(new ItemRoster(), null));
    }

    [Fact]
    public void UnrelatedRoster_IsNotAppliedByInventory()
    {
        var grain = new EquipmentElement(new ItemObject("grain"));
        var logic = CreateLogic(grain, 5);
        var inventory = new InventoryLogicInterface(null, null);
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
