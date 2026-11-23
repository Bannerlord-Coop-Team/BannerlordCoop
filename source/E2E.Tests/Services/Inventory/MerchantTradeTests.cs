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
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Inventory;

/// <summary>
/// Covers two players trading with the same merchant, where the second trade was built from a stale view.
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

        // Replication is held back so it cannot touch B's captured snapshot, which shares the client's roster arrays
        Server.Call(() =>
        {
            Server.SimulateMessage(clientA.NetPeer, tradeA);
            Server.SimulateMessage(clientB.NetPeer, tradeB);
        }, WithoutNetworkDelivery());

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
                soldItems));
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
