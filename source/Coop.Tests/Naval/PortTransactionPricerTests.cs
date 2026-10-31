#if COOP_NAVAL
using Coop.Naval.PortScreen;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Interfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using static Coop.Tests.Naval.PortTestObjects;
using static NavalDLC.ViewModelCollection.Port.PortScreenHandlers.PortScreenHandler;

namespace Coop.Tests.Naval;

/// <summary>
/// The server prices a port trade or repair the way it applies it, so its affordability check matches what it charges.
/// </summary>
public class PortTransactionPricerTests
{
    private readonly FakeShipCostInterface shipCosts = new FakeShipCostInterface();
    private readonly PartyBase player = CreateParty();
    private readonly PartyBase port = CreateParty();
    private readonly PortTransactionPricer pricer;

    public PortTransactionPricerTests()
    {
        pricer = new PortTransactionPricer(shipCosts);
    }

    [Fact]
    public void SaleProceeds_PayForThePurchase()
    {
        var bought = CreateShip(port);
        var sold = CreateShip(player);
        shipCosts.TradeValues[(bought, player)] = 1000;
        shipCosts.TradeValues[(sold, port)] = 800;

        Assert.Equal(200, pricer.GetTradeCost(Changes(buy: new() { bought }, sell: new() { sold })));
    }

    // The server only charges for ships that are still where the client saw them
    [Fact]
    public void ShipsNoLongerAvailable_AreNotPriced()
    {
        var goneFromPort = CreateShip(CreateParty());
        var goneFromParty = CreateShip(CreateParty());
        shipCosts.TradeValues[(goneFromPort, player)] = 500;
        shipCosts.TradeValues[(goneFromParty, port)] = 500;

        Assert.Equal(0, pricer.GetTradeCost(Changes(buy: new() { goneFromPort }, sell: new() { goneFromParty })));
    }

    // The server buys before it upgrades, so an upgrade on a ship bought in the same confirm is charged too
    [Fact]
    public void UpgradeAndRepairOfABoughtShip_ArePriced()
    {
        var bought = CreateShip(port);
        var piece = CreatePiece();
        shipCosts.TradeValues[(bought, player)] = 1000;
        shipCosts.RepairCosts[bought] = 50;
        shipCosts.UpgradeCosts[(bought, piece)] = 300;

        var changes = Changes(
            buy: new() { bought },
            repair: new() { bought },
            pieces: new() { new ShipUpgradePieceInfo(bought, "sail", piece) });

        Assert.Equal(1350, pricer.GetTradeCost(changes));
    }

    // The server sells before it upgrades, so a ship sold in the same confirm isn't repaired or upgraded
    [Fact]
    public void UpgradeAndRepairOfASoldShip_AreNotPriced()
    {
        var sold = CreateShip(player);
        var piece = CreatePiece();
        shipCosts.TradeValues[(sold, port)] = 800;
        shipCosts.RepairCosts[sold] = 50;
        shipCosts.UpgradeCosts[(sold, piece)] = 300;

        var changes = Changes(
            sell: new() { sold },
            repair: new() { sold },
            pieces: new() { new ShipUpgradePieceInfo(sold, "sail", piece) });

        Assert.Equal(-800, pricer.GetTradeCost(changes));
    }

    // A removed piece costs nothing
    [Fact]
    public void RemovedPiece_IsNotPriced()
    {
        var owned = CreateShip(player);

        Assert.Equal(0, pricer.GetTradeCost(Changes(pieces: new() { new ShipUpgradePieceInfo(owned, "sail", null) })));
    }

    [Fact]
    public void Repair_PricesOnlyDamagedShips()
    {
        var damaged = CreateShip(player, hitPoints: 40f);
        var intact = CreateShip(player);
        shipCosts.RepairCosts[damaged] = 120;
        shipCosts.RepairCosts[intact] = 999;

        Assert.Equal(120, pricer.GetRepairCost(player));
    }

    private PortScreenChanges Changes(
        MBReadOnlyList<Ship> buy = null,
        MBReadOnlyList<Ship> sell = null,
        MBReadOnlyList<Ship> repair = null,
        MBReadOnlyList<ShipUpgradePieceInfo> pieces = null) => new PortScreenChanges(
            player,
            port,
            null,
            null,
            null,
            buy ?? new MBReadOnlyList<Ship>(),
            sell ?? new MBReadOnlyList<Ship>(),
            repair ?? new MBReadOnlyList<Ship>(),
            new MBReadOnlyList<ShipRenameInfo>(),
            pieces ?? new MBReadOnlyList<ShipUpgradePieceInfo>(),
            new MBReadOnlyList<ShipFigureheadInfo>(),
            new MBReadOnlyList<Ship>());
}
#endif
