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

        var grainId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var shieldId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyAId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var partyBId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        var merchantRosterId = SetUpMerchant(settlementId, townId, new[] { partyAId, partyBId }, (grainId, 10), (shieldId, 5));
        SetUpCarriedItems(partyAId, grainId, 8);
        SetUpCarriedItems(partyBId, grainId, 4);

        // Trades travel by wire, so the server matches its own items against the party rosters instead of the client's
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                foreach (var (itemId, stringId) in new[] { (grainId, "stale_trade_grain"), (shieldId, "stale_trade_shield") })
                {
                    var item = instance.GetRegisteredObject<ItemObject>(itemId);
                    item.StringId = stringId;
                    MBObjectManager.Instance.RegisterObject(item);
                }
            });
        }

        // B finishes trading against the opening stock and gold, but its commit reaches the server after A's
        var tradeB = CaptureTrade(clientB, merchantRosterId, townId, partyBId,
            bought: Array.Empty<(string, int, int)>(),
            sold: new[] { (grainId, 4, 40) },
            totalAmount: -40);

        // A buys 2 shields for 100 and sells 8 grain for 180, so the merchant pays 80
        var tradeA = CaptureTrade(clientA, merchantRosterId, townId, partyAId,
            bought: new[] { (shieldId, 2, 100) },
            sold: new[] { (grainId, 8, 180) },
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
            var merchantRoster = Server.GetRegisteredObject<ItemRoster>(merchantRosterId);
            Assert.Equal(22, merchantRoster.GetItemNumber(Server.GetRegisteredObject<ItemObject>(grainId)));
            Assert.Equal(3, merchantRoster.GetItemNumber(Server.GetRegisteredObject<ItemObject>(shieldId)));
            Assert.Equal(0, Server.GetRegisteredObject<Town>(townId).Gold);
            Assert.Equal(1080, Server.GetRegisteredObject<MobileParty>(partyAId).LeaderHero.Gold);
            Assert.Equal(1020, Server.GetRegisteredObject<MobileParty>(partyBId).LeaderHero.Gold);
        });
    }

    [Fact]
    public void BuyingIntoAnEmptyPartyRoster_ServerAppliesTheTrade()
    {
        var client = TestEnvironment.Clients.First();

        var grainId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        // Same state as a player who sold everything they carried
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                // Roster packets resolve items through the native catalog, as a loaded campaign does
                var grain = instance.GetRegisteredObject<ItemObject>(grainId);
                grain.StringId = "empty_roster_grain";
                MBObjectManager.Instance.RegisterObject(grain);

                using (instance == Server ? null : new AllowedThread())
                {
                    var roster = instance.GetRegisteredObject<MobileParty>(partyId).ItemRoster;
                    roster.Clear();
                    Assert.Equal(0, roster.Count);
                }
            });
        }

        var merchantRosterId = SetUpMerchant(settlementId, townId, new[] { partyId }, (grainId, 10));

        // Buys 1 grain for 10 with nothing in the party's inventory yet
        var trade = CaptureTrade(client, merchantRosterId, townId, partyId,
            bought: new[] { (grainId, 1, 10) },
            sold: Array.Empty<(string, int, int)>(),
            totalAmount: 10);

        // The command leaves serialized, then the done-logic patch resets the screen until the server answers
        byte[]? wire = null;
        client.Call(() =>
        {
            wire = client.SerializeForWire<IMessage>(trade);
            using (new AllowedThread())
            {
                var grain = new EquipmentElement(client.GetRegisteredObject<ItemObject>(grainId));
                client.GetRegisteredObject<ItemRoster>(merchantRosterId).AddToCounts(grain, 1);
                client.GetRegisteredObject<MobileParty>(partyId).ItemRoster.AddToCounts(grain, -1);
            }
        });

        Server.Call(() => Server.SimulateMessage(client.NetPeer, wire!));
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            var grain = Server.GetRegisteredObject<ItemObject>(grainId);
            Assert.Equal(9, Server.GetRegisteredObject<ItemRoster>(merchantRosterId).GetItemNumber(grain));
        });

        // The client's merchant stock was seeded locally, so only the player's side is compared there
        foreach (var instance in new[] { Server, client })
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(1, party.ItemRoster.GetItemNumber(instance.GetRegisteredObject<ItemObject>(grainId)));
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

        var merchantRosterId = SetUpMerchant(settlementId, townId, new[] { partyId });

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

        var trade = CaptureTrade(client, merchantRosterId, townId, partyId,
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
        var trader = TestEnvironment.Clients.First();

        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var townId = TestEnvironment.CreateRegisteredObject<Town>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        string? rosterId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetId(Server.GetRegisteredObject<Settlement>(settlementId).Party.ItemRoster, out rosterId));

            var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
            hero.Gold = 1000;
            Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
            Server.Resolve<ISessionTradePlayerDataInterface>().AddPlayerKeys(heroId);

            Server.GetRegisteredObject<ItemRoster>(rosterId!).Clear();
            var partyRoster = Server.GetRegisteredObject<MobileParty>(partyId).ItemRoster;
            partyRoster.Clear();
            partyRoster.AddToCounts(DefaultItems.Hides, 5);

            var town = Server.GetRegisteredObject<Town>(townId);
            town.ChangeGold(1000 - town.Gold);
        });
        var merchantRosterId = rosterId!;
        TestEnvironment.FlushCoalescer();
        AssertHidesSold(merchantRosterId, townId, partyId, sold: 0);

        // The join save holds the server's game thread, so the commits wait there and no update reaches the trader,
        // whose screen resets to its 5 hides after each done
        var router = Server.Resolve<TestNetworkRouter>();
        router.ReceiveContext = TestNetworkReceiveContext.PollerThread;
        for (int i = 0; i < 3; i++)
        {
            SellHidesOnTrader(trader, merchantRosterId, townId, partyId, amount: 5, price: 50);
        }
        router.ReceiveContext = TestNetworkReceiveContext.GameThread;

        Assert.Equal(3, trader.NetworkSentMessages.GetMessages<CompleteTrade>().Count());
        Assert.Equal(3, Server.PendingGameThreadActionCount);

        Server.PumpGameThread();
        TestEnvironment.FlushCoalescer();

        AssertHidesSold(merchantRosterId, townId, partyId, sold: 5);
        var sale = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkTradeCompleted>(),
            completed => completed.SoldItems?.Length > 0);
        var (soldHides, soldPrice) = Assert.Single(sale.SoldItems);
        Assert.Equal(5, soldHides.Amount);
        Assert.Equal(50, soldPrice);
    }

    // Each hide sold moves 10 gold from the town to the trader
    private void AssertHidesSold(string merchantRosterId, string townId, string partyId, int sold)
    {
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(
                    (PartyHides: 5 - sold, MerchantHides: sold, TraderGold: 1000 + (10 * sold), TownGold: 1000 - (10 * sold)),
                    (party.ItemRoster.GetItemNumber(DefaultItems.Hides),
                        instance.GetRegisteredObject<ItemRoster>(merchantRosterId).GetItemNumber(DefaultItems.Hides),
                        party.LeaderHero.Gold,
                        instance.GetRegisteredObject<Town>(townId).Gold));
            });
        }
    }

    // Commits a sale the way the done logic patch does: the screen's rosters are sent, then put back as they were
    private void SellHidesOnTrader(EnvironmentInstance trader, string merchantRosterId, string townId, string partyId, int amount, int price)
    {
        trader.Call(() =>
        {
            var item = DefaultItems.Hides;
            var merchantRoster = trader.GetRegisteredObject<ItemRoster>(merchantRosterId);
            var party = trader.GetRegisteredObject<MobileParty>(partyId);
            var town = trader.GetRegisteredObject<Town>(townId);
            var initialPartyRoster = party.ItemRoster.ToArray();
            var sold = new List<(ItemRosterElement, int)> { (new ItemRosterElement(item, amount), price) };

            using (new AllowedThread())
            {
                merchantRoster.AddToCounts(item, amount);
                party.ItemRoster.AddToCounts(item, -amount);

                trader.Resolve<IMessageBroker>().Publish(this, new TradeAttempted(
                    merchantRoster,
                    party.ItemRoster,
                    isTrading: true,
                    canGainXpFromDiscarding: false,
                    InventoryScreenHelper.InventoryMode.Trade,
                    party.LeaderHero,
                    party.LeaderHero.CharacterObject,
                    -price,
                    town.Gold,
                    party,
                    currentMobileParty: null,
                    town,
                    new List<(ItemRosterElement, int)>(),
                    sold,
                    initialPlayerRoster: initialPartyRoster));

                merchantRoster.AddToCounts(item, -amount);
                party.ItemRoster.AddToCounts(item, amount);
            }
        });
    }

    // Gives the merchant the same opening stock and 100 gold everywhere, and each trading party 1000 gold
    private string SetUpMerchant(string settlementId, string townId, string[] partyIds, params (string itemId, int amount)[] stock)
    {
        string? merchantRosterId = null;
        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.True(Server.ObjectManager.TryGetId(settlement.Party.ItemRoster, out merchantRosterId));

            foreach (var partyId in partyIds)
            {
                var hero = Server.GetRegisteredObject<MobileParty>(partyId).LeaderHero;
                hero.Gold = 1000;

                // Trade skill bookkeeping expects every trading player to have been registered on join
                Assert.True(Server.ObjectManager.TryGetId(hero, out var heroId));
                Server.Resolve<ISessionTradePlayerDataInterface>().AddPlayerKeys(heroId);
            }
        });

        // The harness cannot replicate test-made items, so each client is given the same opening stock and gold locally
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using (instance == Server ? null : new AllowedThread())
                {
                    var merchantRoster = instance.GetRegisteredObject<ItemRoster>(merchantRosterId!);
                    foreach (var (itemId, amount) in stock)
                    {
                        merchantRoster.AddToCounts(new EquipmentElement(instance.GetRegisteredObject<ItemObject>(itemId)), amount);
                    }

                    var town = instance.GetRegisteredObject<Town>(townId);
                    town.ChangeGold(100 - town.Gold);
                }
            });
        }

        return merchantRosterId!;
    }

    // Gives a party items to sell on every instance, the same way the merchant's stock is given
    private void SetUpCarriedItems(string partyId, string itemId, int amount)
    {
        foreach (var instance in TestEnvironment.Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                using (instance == Server ? null : new AllowedThread())
                {
                    var item = new EquipmentElement(instance.GetRegisteredObject<ItemObject>(itemId));
                    instance.GetRegisteredObject<MobileParty>(partyId).ItemRoster.AddToCounts(item, amount);
                }
            });
        }
    }

    // Commits a trade on the client the way the done-logic patch does, and returns the undelivered server command
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
        client.Call(() =>
        {
            var merchantRoster = client.GetRegisteredObject<ItemRoster>(merchantRosterId);
            var town = client.GetRegisteredObject<Town>(townId);
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            var initialPartyRoster = party.ItemRoster.ToArray();

            var boughtItems = ToTradeItems(client, bought);
            var soldItems = ToTradeItems(client, sold);

            // The client's screen moves the items locally before committing. The undelivered command shares the
            // roster arrays instead of holding a serialized copy, so they are not reset the way the done-logic patch does
            using (new AllowedThread())
            {
                foreach (var (item, _) in boughtItems)
                {
                    merchantRoster.AddToCounts(item.EquipmentElement, -item.Amount);
                    party.ItemRoster.AddToCounts(item.EquipmentElement, item.Amount);
                }
                foreach (var (item, _) in soldItems)
                {
                    merchantRoster.AddToCounts(item.EquipmentElement, item.Amount);
                    party.ItemRoster.AddToCounts(item.EquipmentElement, -item.Amount);
                }
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
        }, WithoutNetworkDelivery());

        return Assert.Single(client.NetworkSentMessages.GetMessages<CompleteTrade>());
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
