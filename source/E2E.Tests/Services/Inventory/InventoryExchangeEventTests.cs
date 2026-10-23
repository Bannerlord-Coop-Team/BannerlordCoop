using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using Helpers;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Quest = TaleWorlds.CampaignSystem.Issues.ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

namespace E2E.Tests.Services.Inventory;

/// <summary>
/// Covers the player inventory exchange event, which vanilla raises from the inventory screen's done logic that Coop replaces.
/// </summary>
public class InventoryExchangeEventTests : IDisposable
{
    private const int BoughtGrain = 4;
    private const int GrainPrice = 40;
    private const int SoldHides = 2;
    private const int HidesPrice = 30;

    private readonly string partyId;
    private readonly string merchantRosterId;
    private readonly string townId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Trader => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();

    public InventoryExchangeEventTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        string? rosterId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetId(Server.GetRegisteredObject<Settlement>(settlementId).Party.ItemRoster, out rosterId));

            var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
            hero.Gold = 1000;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Server.Resolve<ISessionTradePlayerDataInterface>().AddPlayerKeys(heroId);

            var merchantRoster = Server.GetRegisteredObject<ItemRoster>(rosterId!);
            merchantRoster.Clear();
            merchantRoster.AddToCounts(DefaultItems.Grain, 10);

            var partyRoster = Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster;
            partyRoster.Clear();
            partyRoster.AddToCounts(DefaultItems.Hides, 5);

            var town = Server.GetRegisteredObject<Town>(townId);
            town.ChangeGold(1000 - town.Gold);
        });
        merchantRosterId = rosterId!;

        // Every client starts from the server's stock, as after a join
        TestEnvironment.FlushCoalescer();
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.Equal(10, client.GetRegisteredObject<ItemRoster>(merchantRosterId).GetItemNumber(DefaultItems.Grain));
                Assert.Equal(0, client.GetRegisteredObject<MobileParty>(partyId).ItemRoster.GetItemNumber(DefaultItems.Grain));
                Assert.Equal(5, client.GetRegisteredObject<MobileParty>(partyId).ItemRoster.GetItemNumber(DefaultItems.Hides));
            });
        }

        Trader.Call(() => Campaign.Current.MainParty = Trader.GetRegisteredObject<MobileParty>(partyId));
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void TradeOnTheClient_RaisesTheEventOnceOnTheTradersGameWithTheTradedItems()
    {
        var exchanges = RecordExchanges();

        TradeOnTrader(isTrading: true);

        Assert.Empty(exchanges[Server]);
        Assert.Empty(exchanges[OtherClient]);

        var exchange = Assert.Single(exchanges[Trader]);
        Assert.True(exchange.IsTrading);
        AssertSingleItem(exchange.Purchased, "grain", BoughtGrain, GrainPrice);
        AssertSingleItem(exchange.Sold, "hides", SoldHides, HidesPrice);

        // Raised after the server's roster update reached the trader, not from the screen's reset roster
        Assert.Equal(BoughtGrain, exchange.GrainInMainParty);
    }

    [Fact]
    public void TradeOnTheClient_ArmyNeedsSuppliesRecountsTheTradedRoster()
    {
        Quest? quest = null;
        Trader.Call(() =>
        {
            quest = (Quest)RuntimeHelpers.GetUninitializedObject(typeof(Quest));
            quest._requestedGrainAmount = 10;
            quest.RegisterEvents();
        });

        TradeOnTrader(isTrading: true);

        Assert.Equal(BoughtGrain, quest!._currentGrainAmount);
    }

    [Fact]
    public void TradeAfterAnotherPlayerEmptiedTheStock_ReportsOnlyThePurchaseTheServerKept()
    {
        var exchanges = RecordExchanges();

        // Another trade left 2 grain, which the trader's screen has not seen yet
        Server.Call(() => Server.GetRegisteredObject<ItemRoster>(merchantRosterId).AddToCounts(DefaultItems.Grain, -8));

        TradeOnTrader(isTrading: true);

        var exchange = Assert.Single(exchanges[Trader]);
        AssertSingleItem(exchange.Purchased, "grain", 2, GrainPrice / 2);
        Assert.Equal(2, exchange.GrainInMainParty);
    }

    [Fact]
    public void LootScreen_ReportsTheItemsPutBackNotTheLootLeftBehind()
    {
        var exchanges = RecordExchanges();

        TradeOnTrader(isTrading: false);

        var exchange = Assert.Single(exchanges[Trader]);
        Assert.False(exchange.IsTrading);
        AssertSingleItem(exchange.Purchased, "grain", BoughtGrain, GrainPrice);
        AssertSingleItem(exchange.Sold, "hides", SoldHides, HidesPrice);
        Assert.Equal(BoughtGrain, exchange.GrainInMainParty);
    }

    private Dictionary<EnvironmentInstance, List<Exchange>> RecordExchanges()
    {
        var exchanges = new Dictionary<EnvironmentInstance, List<Exchange>>();
        foreach (var instance in new[] { Server, Trader, OtherClient })
        {
            var recorded = new List<Exchange>();
            exchanges[instance] = recorded;
            instance.Call(() => CampaignEvents.PlayerInventoryExchangeEvent.AddNonSerializedListener(this, (purchased, sold, isTrading) =>
                recorded.Add(new Exchange(purchased, sold, isTrading, MobileParty.MainParty?.ItemRoster.GetItemNumber(DefaultItems.Grain) ?? -1))));
        }

        return exchanges;
    }

    private static void AssertSingleItem(List<(ItemRosterElement, int)> items, string itemId, int amount, int price)
    {
        var (element, itemPrice) = Assert.Single(items);
        Assert.Equal(itemId, element.EquipmentElement.Item.StringId);
        Assert.Equal(amount, element.Amount);
        Assert.Equal(price, itemPrice);
    }

    // Commits the trader's screen the way the done logic patch does: the screen's rosters are sent, then put back as they
    // were. Without trading the merchant's stock stands in for a loot pile the trader takes grain from and puts hides on.
    private void TradeOnTrader(bool isTrading)
    {
        Trader.Call(() =>
        {
            var merchantRoster = Trader.GetRegisteredObject<ItemRoster>(merchantRosterId);
            var party = Trader.GetRegisteredObject<MobileParty>(partyId);
            var town = Trader.GetRegisteredObject<Town>(townId);
            var initialPartyRoster = party.ItemRoster.ToArray();

            var bought = new List<(ItemRosterElement, int)> { (new ItemRosterElement(DefaultItems.Grain, BoughtGrain), GrainPrice) };
            var sold = new List<(ItemRosterElement, int)> { (new ItemRosterElement(DefaultItems.Hides, SoldHides), HidesPrice) };

            using (new AllowedThread())
            {
                MoveScreenItems(merchantRoster, party.ItemRoster, bought, sold, direction: 1);

                Trader.Resolve<IMessageBroker>().Publish(this, new TradeAttempted(
                    merchantRoster,
                    party.ItemRoster,
                    isTrading,
                    canGainXpFromDiscarding: !isTrading,
                    isTrading ? InventoryScreenHelper.InventoryMode.Trade : InventoryScreenHelper.InventoryMode.Loot,
                    party.LeaderHero,
                    party.LeaderHero.CharacterObject,
                    isTrading ? GrainPrice - HidesPrice : 0,
                    isTrading ? town.Gold : 0,
                    party,
                    currentMobileParty: null,
                    isTrading ? town : null,
                    bought,
                    sold,
                    initialPlayerRoster: initialPartyRoster));

                MoveScreenItems(merchantRoster, party.ItemRoster, bought, sold, direction: -1);
            }

            Assert.Equal(0, party.ItemRoster.GetItemNumber(DefaultItems.Grain));
        });
    }

    private static void MoveScreenItems(
        ItemRoster merchantRoster,
        ItemRoster partyRoster,
        List<(ItemRosterElement, int)> bought,
        List<(ItemRosterElement, int)> sold,
        int direction)
    {
        foreach (var (item, _) in bought)
        {
            merchantRoster.AddToCounts(item.EquipmentElement, -direction * item.Amount);
            partyRoster.AddToCounts(item.EquipmentElement, direction * item.Amount);
        }
        foreach (var (item, _) in sold)
        {
            merchantRoster.AddToCounts(item.EquipmentElement, direction * item.Amount);
            partyRoster.AddToCounts(item.EquipmentElement, -direction * item.Amount);
        }
    }

    private sealed record Exchange(
        List<(ItemRosterElement, int)> Purchased,
        List<(ItemRosterElement, int)> Sold,
        bool IsTrading,
        int GrainInMainParty);
}
