using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Inventory.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using HarmonyLib;
using Helpers;
using LiteNetLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Inventory;

/// <summary>
/// Covers merchant trades built from a stale view, by another player or by the same player before their last trade applied.
/// </summary>
public class MerchantTradeTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;

    public MerchantTradeTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void TwoClientsTradingAtTheSameMerchant_BothTradesReachStockAndGold()
    {
        var clientA = TestEnvironment.Clients.First();
        var clientB = TestEnvironment.Clients.Last();

        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyAId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var partyBId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // A carries 8 grain and B carries 4, and the merchant has 10 grain, 5 hides and 100 gold, on every instance
        string? merchantRosterId = null;
        string? grainId = null;
        string? hidesId = null;
        Server.Call(() =>
        {
            merchantRosterId = SetUpMarket(settlementId, townId, townGold: 100, (DefaultItems.Grain, 10), (DefaultItems.Hides, 5));
            foreach (var (partyId, carriedGrain) in new[] { (partyAId, 8), (partyBId, 4) })
            {
                SetUpTradingHero(partyId, 1000);
                SetRoster(Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster, (DefaultItems.Grain, carriedGrain));
            }

            // An item gets its id once it reaches a synced roster
            Assert.True(Server.ObjectManager.TryGetId(DefaultItems.Grain, out grainId));
            Assert.True(Server.ObjectManager.TryGetId(DefaultItems.Hides, out hidesId));
        });
        TestEnvironment.FlushCoalescer();

        // B finishes trading against the opening stock and gold, but its commit reaches the server after A's
        var tradeB = CaptureTrade(clientB, merchantRosterId!, townId, partyBId,
            bought: Array.Empty<(string, int, int)>(),
            sold: new[] { (grainId!, 4, 40) },
            totalAmount: -40);

        // A buys 2 hides for 100 and sells 8 grain for 180, so the merchant pays 80
        var tradeA = CaptureTrade(clientA, merchantRosterId!, townId, partyAId,
            bought: new[] { (hidesId!, 2, 100) },
            sold: new[] { (grainId!, 8, 180) },
            totalAmount: -80);

        // Both commands leave serialized before the server applies either, so B's still holds the opening stock and gold
        byte[]? wireA = null;
        byte[]? wireB = null;
        clientA.Call(() => wireA = clientA.SerializeForWire<IMessage>(tradeA));
        clientB.Call(() => wireB = clientB.SerializeForWire<IMessage>(tradeB));

        Server.Call(() =>
        {
            Server.SimulateMessage(clientA.NetPeer, wireA!);
            Server.SimulateMessage(clientB.NetPeer, wireB!);
        });

        // The merchant only had 20 left after paying A, so B is paid 20 instead of the 40 it saw
        Server.Call(() =>
        {
            var merchantRoster = Server.GetRegisteredObject<ItemRoster>(merchantRosterId!);
            Assert.Equal(22, merchantRoster.GetItemNumber(DefaultItems.Grain));
            Assert.Equal(3, merchantRoster.GetItemNumber(DefaultItems.Hides));
            Assert.Equal(0, Server.GetRegisteredObject<Town>(townId).Gold);
            Assert.Equal(1080, Server.GetRegisteredObject<MobileParty>(partyAId).LeaderHero.Gold);
            Assert.Equal(1020, Server.GetRegisteredObject<MobileParty>(partyBId).LeaderHero.Gold);
        });
    }

    [Fact]
    public void BuyingIntoAnEmptyPartyRoster_ServerAppliesTheTrade()
    {
        var client = TestEnvironment.Clients.First();

        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // The party carries nothing, the same state as a player who sold everything they carried
        string? merchantRosterId = null;
        string? grainId = null;
        Server.Call(() =>
        {
            merchantRosterId = SetUpMarket(settlementId, townId, townGold: 100, (DefaultItems.Grain, 10));
            SetUpTradingHero(partyId, 1000);
            SetRoster(Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster);
            Assert.True(Server.ObjectManager.TryGetId(DefaultItems.Grain, out grainId));
        });
        TestEnvironment.FlushCoalescer();
        client.Call(() => Assert.Equal(0, client.GetRegisteredObject<MobileParty>(partyId).ItemRoster.Count));

        // Buys 1 grain for 10 with nothing in the party's inventory yet
        var trade = CaptureTrade(client, merchantRosterId!, townId, partyId,
            bought: new[] { (grainId!, 1, 10) },
            sold: Array.Empty<(string, int, int)>(),
            totalAmount: 10);

        // The command leaves serialized, then the done-logic patch resets the screen until the server answers
        byte[]? wire = null;
        client.Call(() =>
        {
            wire = client.SerializeForWire<IMessage>(trade);
            using (new AllowedThread())
            {
                client.GetRegisteredObject<ItemRoster>(merchantRosterId!).AddToCounts(DefaultItems.Grain, 1);
                client.GetRegisteredObject<MobileParty>(partyId).ItemRoster.AddToCounts(DefaultItems.Grain, -1);
            }
        });

        Server.Call(() => Server.SimulateMessage(client.NetPeer, wire!));
        TestEnvironment.FlushCoalescer();

        foreach (var instance in new[] { Server, client })
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(1, party.ItemRoster.GetItemNumber(DefaultItems.Grain));
                Assert.Equal(9, instance.GetRegisteredObject<ItemRoster>(merchantRosterId!).GetItemNumber(DefaultItems.Grain));
                Assert.Equal(990, party.LeaderHero.Gold);
                Assert.Equal(110, instance.GetRegisteredObject<Town>(townId).Gold);
            });
        }
    }

    [Fact]
    public void EquippingDuringATrade_ServerAndEveryClientGetTheSlot()
    {
        var client = TestEnvironment.Clients.First();

        var armorId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var oldArmorId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Trade equipment resolves items through the native catalog, as a loaded campaign does
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var armor = instance.GetRegisteredObject<ItemObject>(armorId);
                armor.StringId = "trade_equipped_armor";
                MBObjectManager.Instance.RegisterObject(armor);

                using (new AllowedThread())
                {
                    var oldArmor = instance.GetRegisteredObject<ItemObject>(oldArmorId);
                    foreach (var item in new[] { armor, oldArmor })
                    {
                        item.Type = ItemObject.ItemTypeEnum.BodyArmor;
                        item.Name = new TextObject(item.StringId);
                    }
                    instance.GetRegisteredObject<MobileParty>(partyId).LeaderHero.BattleEquipment[EquipmentIndex.Body] = new EquipmentElement(oldArmor);
                }
            });
        }

        string? merchantRosterId = null;
        Server.Call(() =>
        {
            merchantRosterId = SetUpMarket(settlementId, townId, townGold: 100);
            SetUpTradingHero(partyId, 1000);
        });
        TestEnvironment.FlushCoalescer();

        // The inventory screen equips the armor over the old one, then Done is pressed without buying anything
        client.Call(() =>
        {
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            var armor = client.GetRegisteredObject<ItemObject>(armorId);
            var logic = new InventoryLogic(party, party.LeaderHero.CharacterObject, null);
            using (new AllowedThread())
            {
                logic._rosters[0] = new ItemRoster();
                logic._rosters[1] = new ItemRoster();
                logic._rosters[1].AddToCounts(armor, 1);
                logic._useBasePrices = true;
            }

            logic.AddTransferCommand(TransferCommand.Transfer(1, InventoryLogic.InventorySide.PlayerInventory, InventoryLogic.InventorySide.BattleEquipment,
                new ItemRosterElement(armor, 1), EquipmentIndex.None, EquipmentIndex.Body, party.LeaderHero.CharacterObject));

            Assert.Same(armor, party.LeaderHero.BattleEquipment[EquipmentIndex.Body].Item);
        });

        var trade = CaptureTrade(client, merchantRosterId!, townId, partyId,
            bought: Array.Empty<(string, int, int)>(),
            sold: Array.Empty<(string, int, int)>(),
            totalAmount: 0);

        // The equipment travels by wire, so the server resolves its own item instead of the client's
        byte[]? wire = null;
        client.Call(() => wire = client.SerializeForWire<IMessage>(trade));

        Server.Call(() => Server.SimulateMessage(client.NetPeer, wire!));
        TestEnvironment.FlushCoalescer();

        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var hero = instance.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
                Assert.Same(instance.GetRegisteredObject<ItemObject>(armorId), hero.BattleEquipment[EquipmentIndex.Body].Item);
            });
        }
    }

    [Fact]
    public void SellingTheSameHidesAgainWhileAPlayerJoins_PaysAndStocksThemOnce()
    {
        var trade = SetUpJoinWindowTrade(traderGold: 1000);

        // The trader's screen resets to its 5 hides after each done, so it sells them again
        CommitWhileTheServerIsHeld(trade, commits: 3, () => TradeOnTrader(trade, soldHides: 5, boughtGrain: 0));

        AssertTradeState(trade, new TradeState(PartyHides: 0, PartyGrain: 0, MerchantHides: 5, MerchantGrain: 10, TraderGold: 1050, TownGold: 950));
        var sale = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTradeCompleted>(),
            completed => completed.SoldItems?.Length > 0);
        var (soldHides, soldPrice) = Assert.Single(sale.SoldItems);
        Assert.Equal(5, soldHides.Amount);
        Assert.Equal(50, soldPrice);
    }

    [Fact]
    public void BuyingAgainWithGoldAlreadySpentWhileAPlayerJoins_IsRefused()
    {
        var trade = SetUpJoinWindowTrade(traderGold: 30);

        // The trader's screen still shows its 30 gold after the first purchase, so it buys again
        CommitWhileTheServerIsHeld(trade, commits: 2, () => TradeOnTrader(trade, soldHides: 0, boughtGrain: 1));

        AssertTradeState(trade, new TradeState(PartyHides: 5, PartyGrain: 1, MerchantHides: 0, MerchantGrain: 9, TraderGold: 10, TownGold: 1020));
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTradeCompleted>());
    }

    // The trader carries 5 hides, and the merchant has 10 grain and 1000 gold, on the server and every client
    private JoinWindowTrade SetUpJoinWindowTrade(int traderGold)
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        string? rosterId = null;
        Server.Call(() =>
        {
            rosterId = SetUpMarket(settlementId, townId, townGold: 1000, (DefaultItems.Grain, 10));
            SetUpTradingHero(partyId, traderGold);
            SetRoster(Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster, (DefaultItems.Hides, 5));
        });
        TestEnvironment.FlushCoalescer();

        var trade = new JoinWindowTrade(TestEnvironment.Clients.First(), rosterId!, townId, partyId);
        AssertTradeState(trade, new TradeState(PartyHides: 5, PartyGrain: 0, MerchantHides: 0, MerchantGrain: 10, TraderGold: traderGold, TownGold: 1000));
        return trade;
    }

    // The join save holds the server's game thread, so the commits wait there and no update reaches the trader
    private void CommitWhileTheServerIsHeld(JoinWindowTrade trade, int commits, Action commit)
    {
        var opening = ReadTradeState(Server, trade);

        var router = Server.Resolve<TestNetworkRouter>();
        router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        for (int i = 0; i < commits; i++)
        {
            commit();
        }
        router.ReceiveContext = TestNetworkReceiveContext.GameThread;
        AssertTradeState(trade, opening);

        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();
    }

    private void AssertTradeState(JoinWindowTrade trade, TradeState expected)
    {
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            Assert.Equal(expected, ReadTradeState(instance, trade));
        }
    }

    private static TradeState ReadTradeState(EnvironmentInstance instance, JoinWindowTrade trade)
    {
        TradeState? state = null;
        instance.Call(() =>
        {
            var party = instance.GetRegisteredObject<MobileParty>(trade.PartyId);
            var merchantRoster = instance.GetRegisteredObject<ItemRoster>(trade.MerchantRosterId);
            state = new TradeState(
                PartyHides: party.ItemRoster.GetItemNumber(DefaultItems.Hides),
                PartyGrain: party.ItemRoster.GetItemNumber(DefaultItems.Grain),
                MerchantHides: merchantRoster.GetItemNumber(DefaultItems.Hides),
                MerchantGrain: merchantRoster.GetItemNumber(DefaultItems.Grain),
                TraderGold: party.LeaderHero.Gold,
                TownGold: instance.GetRegisteredObject<Town>(trade.TownId).Gold);
        });
        return state!;
    }

    // Commits the trader's screen, then puts its rosters back as the done-logic patch does until the server answers.
    // Hides sell for 10 each and grain costs 20 each
    private void TradeOnTrader(JoinWindowTrade trade, int soldHides, int boughtGrain)
    {
        var trader = trade.Trader;
        trader.Call(() =>
        {
            var bought = new List<(ItemRosterElement, int)>();
            if (boughtGrain > 0) bought.Add((new ItemRosterElement(DefaultItems.Grain, boughtGrain), 20 * boughtGrain));
            var sold = new List<(ItemRosterElement, int)>();
            if (soldHides > 0) sold.Add((new ItemRosterElement(DefaultItems.Hides, soldHides), 10 * soldHides));

            CommitScreen(trader, trade.MerchantRosterId, trade.TownId, trade.PartyId, bought, sold, (20 * boughtGrain) - (10 * soldHides));

            using (new AllowedThread())
            {
                MoveScreenItems(
                    trader.GetRegisteredObject<ItemRoster>(trade.MerchantRosterId),
                    trader.GetRegisteredObject<MobileParty>(trade.PartyId).ItemRoster,
                    bought,
                    sold,
                    direction: -1);
            }
        });
    }

    private sealed record JoinWindowTrade(EnvironmentInstance Trader, string MerchantRosterId, string TownId, string PartyId);

    private sealed record TradeState(int PartyHides, int PartyGrain, int MerchantHides, int MerchantGrain, int TraderGold, int TownGold);

    // Runs inside Server.Call. Gives the town's merchant exactly this stock and gold, which replicate to every client
    private string SetUpMarket(string settlementId, string townId, int townGold, params (ItemObject item, int amount)[] stock)
    {
        var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
        Assert.True(Server.ObjectManager.TryGetId(settlement.Party.ItemRoster, out var merchantRosterId));
        SetRoster(Server.GetRegisteredObject<ItemRoster>(merchantRosterId), stock);

        var town = Server.GetRegisteredObject<Town>(townId);
        town.ChangeGold(townGold - town.Gold);
        return merchantRosterId;
    }

    // Runs inside Server.Call. Gives the party's hero gold and registers it the way a trading player's join does
    private void SetUpTradingHero(string partyId, int gold)
    {
        var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
        hero.Gold = gold;

        // Trade skill bookkeeping expects every trading player to have been registered on join
        Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
        Server.Resolve<ISessionTradePlayerDataInterface>().AddPlayerKeys(heroId);
    }

    private static void SetRoster(ItemRoster roster, params (ItemObject item, int amount)[] items)
    {
        roster.Clear();
        foreach (var (item, amount) in items)
        {
            roster.AddToCounts(item, amount);
        }
    }

    // Commits a trade on the client and returns the undelivered server command. The command shares the roster arrays
    // instead of holding a serialized copy, so they are not reset the way the done-logic patch does
    private CompleteTrade CaptureTrade(
        EnvironmentInstance client,
        string merchantRosterId,
        string townId,
        string partyId,
        (string itemId, int amount, int price)[] bought,
        (string itemId, int amount, int price)[] sold,
        int totalAmount)
    {
        client.NetworkSentMessages.Clear();
        client.Call(() => CommitScreen(client, merchantRosterId, townId, partyId,
            ToTradeItems(client, bought), ToTradeItems(client, sold), totalAmount), WithoutNetworkDelivery());

        return Assert.Single(client.NetworkSentMessages.GetMessages<CompleteTrade>());
    }

    // Commits the screen the way the done-logic patch does: the screen moves the items locally, then its rosters are
    // sent with the party roster it opened with. Runs inside the client's Call
    private void CommitScreen(
        EnvironmentInstance client,
        string merchantRosterId,
        string townId,
        string partyId,
        List<(ItemRosterElement, int)> boughtItems,
        List<(ItemRosterElement, int)> soldItems,
        int totalAmount)
    {
        var merchantRoster = client.GetRegisteredObject<ItemRoster>(merchantRosterId);
        var town = client.GetRegisteredObject<Town>(townId);
        var party = client.GetRegisteredObject<MobileParty>(partyId);
        var initialPartyRoster = party.ItemRoster.ToArray();

        using (new AllowedThread())
        {
            MoveScreenItems(merchantRoster, party.ItemRoster, boughtItems, soldItems, direction: 1);
        }

        client.Resolve<IMessageBroker>().Publish(this, new TradeAttempted(
            merchantRoster,
            party.ItemRoster,
            isTrading: true,
            canGainXpFromDiscarding: false,
            InventoryScreenHelper.InventoryMode.Trade,
            party.LeaderHero,
            party.LeaderHero.CharacterObject,
            totalAmount,
            town.Gold,
            party,
            currentMobileParty: null,
            town,
            boughtItems,
            soldItems,
            initialPlayerRoster: initialPartyRoster));
    }

    private static void MoveScreenItems(
        ItemRoster merchantRoster,
        ItemRoster partyRoster,
        List<(ItemRosterElement, int)> boughtItems,
        List<(ItemRosterElement, int)> soldItems,
        int direction)
    {
        foreach (var (item, _) in boughtItems)
        {
            merchantRoster.AddToCounts(item.EquipmentElement, -direction * item.Amount);
            partyRoster.AddToCounts(item.EquipmentElement, direction * item.Amount);
        }
        foreach (var (item, _) in soldItems)
        {
            merchantRoster.AddToCounts(item.EquipmentElement, direction * item.Amount);
            partyRoster.AddToCounts(item.EquipmentElement, -direction * item.Amount);
        }
    }

    private static List<(ItemRosterElement, int)> ToTradeItems(EnvironmentInstance instance, (string itemId, int amount, int price)[] items)
    {
        return items
            .Select(item => (new ItemRosterElement(instance.GetRegisteredObject<ItemObject>(item.itemId), item.amount), item.price))
            .ToList();
    }

    private static IEnumerable<MethodBase> WithoutNetworkDelivery() => new[]
    {
        AccessTools.Method(typeof(TestNetworkRouter), nameof(TestNetworkRouter.SendReliablePayload),
            new[] { typeof(NetPeer), typeof(NetPeer), typeof(byte[]) })
    };
}
