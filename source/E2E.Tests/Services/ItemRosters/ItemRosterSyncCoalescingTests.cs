using Common.Network;
using Common.Network.Coalescing;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemRosters
{
    /// <summary>
    /// End to end tests for the per-tick coalesced ItemRoster sync. The server publishes each AddToCounts
    /// delta, the coalescer sums them per element per tick, and the client replays the merged delta, so a
    /// client converges to the server's count for that element.
    /// </summary>
    public class ItemRosterSyncCoalescingTests : SyncTestBase
    {
        private readonly string ItemRosterId;
        private readonly string ItemId;

        public ItemRosterSyncCoalescingTests(ITestOutputHelper output) : base(output)
        {
            ItemRosterId = TestEnvironment.CreateRegisteredObject<ItemRoster>();
            ItemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        }

        [Fact]
        public void Server_AddToCounts_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var item);
                roster.AddToCounts(new EquipmentElement(item), 5);
            });

            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var item);
                Assert.Equal(5, GetCount(roster, item));
            }
        }

        [Fact]
        public void Server_MultipleDeltasInOneTick_CoalesceToSummedCount()
        {
            // Two AddToCounts on the same element in one tick (+5 then -2 -> the server has 3) collapse into
            // one summed delta on the wire; the client replays it and lands on 3.
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var item);
                var element = new EquipmentElement(item);
                roster.AddToCounts(element, 5);
                roster.AddToCounts(element, -2);
            });

            FlushCoalescer();

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var item);
                Assert.Equal(3, GetCount(roster, item));
            });

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var item);
                Assert.Equal(3, GetCount(roster, item));
            }
        }

        [Fact]
        public void Server_ReduceCount_SyncsToClients()
        {
            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var item);
                roster.AddToCounts(new EquipmentElement(item), 5);
            });
            FlushCoalescer();

            Server.Call(() =>
            {
                Resolve(Server, out var roster, out var item);
                roster.AddToCounts(new EquipmentElement(item), -3);
            });
            FlushCoalescer();

            foreach (var client in Clients)
            {
                Resolve(client, out var roster, out var item);
                Assert.Equal(2, GetCount(roster, item));
            }
        }

        // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server scope.
        private void FlushCoalescer()
        {
            Server.Call(() => Server.Resolve<ISendCoalescer>().Flush(Server.Resolve<INetwork>()));
        }

        private void Resolve(EnvironmentInstance instance, out ItemRoster roster, out ItemObject item)
        {
            Assert.True(instance.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out roster));
            Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(ItemId, out item));
        }

        private static int GetCount(ItemRoster roster, ItemObject item)
        {
            int index = roster.FindIndexOfElement(new EquipmentElement(item));
            return index >= 0 ? roster.GetElementNumber(index) : 0;
        }
    }
}
