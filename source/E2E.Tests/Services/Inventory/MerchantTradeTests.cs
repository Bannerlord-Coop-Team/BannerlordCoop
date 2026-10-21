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

        string? merchantRosterId = null;
        Server.Call(() =>
        {
            var settlement = Server.GetRegisteredObject<Settlement>(settlementId);
            Assert.True(Server.ObjectManager.TryGetId(settlement.Party.ItemRoster, out merchantRosterId));

            foreach (var partyId in new[] { partyAId, partyBId })
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
                    merchantRoster.AddToCounts(new EquipmentElement(instance.GetRegisteredObject<ItemObject>(grainId)), 10);
                    merchantRoster.AddToCounts(new EquipmentElement(instance.GetRegisteredObject<ItemObject>(shieldId)), 5);

                    var town = instance.GetRegisteredObject<Town>(townId);
                    town.ChangeGold(100 - town.Gold);
                }
            });
        }

        // B finishes trading against the opening stock and gold, but its commit reaches the server after A's
        var tradeB = CaptureTrade(clientB, merchantRosterId!, townId, partyBId,
            bought: Array.Empty<(string, int, int)>(),
            sold: new[] { (grainId, 4, 40) },
            totalAmount: -40);

        // A buys 2 shields for 100 and sells 8 grain for 180, so the merchant pays 80
        var tradeA = CaptureTrade(clientA, merchantRosterId!, townId, partyAId,
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
            var merchantRoster = Server.GetRegisteredObject<ItemRoster>(merchantRosterId!);
            Assert.Equal(22, merchantRoster.GetItemNumber(Server.GetRegisteredObject<ItemObject>(grainId)));
            Assert.Equal(3, merchantRoster.GetItemNumber(Server.GetRegisteredObject<ItemObject>(shieldId)));
            Assert.Equal(0, Server.GetRegisteredObject<Town>(townId).Gold);
            Assert.Equal(1080, Server.GetRegisteredObject<MobileParty>(partyAId).LeaderHero.Gold);
            Assert.Equal(1020, Server.GetRegisteredObject<MobileParty>(partyBId).LeaderHero.Gold);
        });
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
