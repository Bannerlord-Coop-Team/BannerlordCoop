using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.ItemRosters.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.ItemRosters.Messages;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
namespace Coop.IntegrationTests.ItemRosters
{
    public class ItemRostersServerTests
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Checks if ItemRosterUpdated triggered on server, triggers UpdateItemRoster on all clients.
        /// </summary>
        [Fact]
        public void ServerReceivesItemRosterUpdated_PublishesUpdateItemRoster_AllClients()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
            }

            var item = TestEnvironment.Server.CreateRegisteredObject<ItemObject>("item1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemObject>("item1");
            }

            var triggerMessage = new ItemRosterUpdated(itemRoster, item, null, 1);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.InternalMessages.GetMessageCount<ItemRosterUpdated>());

            // The update is coalesced, so it only reaches clients once the server flushes for the tick.
            FlushCoalescer(server);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdateItemRoster>());
            }
        }

        /// <summary>
        /// Checks if ItemRosterCleared triggered on server, triggers ClearItemRoster on all clients.
        /// </summary>
        [Fact]
        public void ServerReceivesItemRosterCleared_PublishesClearItemRoster_AllClients()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
            }

            var triggerMessage = new ItemRosterCleared(itemRoster);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.InternalMessages.GetMessageCount<ItemRosterCleared>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClearItemRoster>());
            }
        }

        /// <summary>
        /// Repeated updates to the same roster element within a tick collapse into one summed send.
        /// </summary>
        [Fact]
        public void ServerCoalescesItemRosterUpdates_SendsSingleSummedUpdate()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            var item = TestEnvironment.Server.CreateRegisteredObject<ItemObject>("item1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
                client.CreateRegisteredObject<ItemObject>("item1");
            }

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, new ItemRosterUpdated(itemRoster, item, null, 2));
            server.SimulateMessage(this, new ItemRosterUpdated(itemRoster, item, null, 3));

            // Nothing goes out until the tick flush.
            Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkItemRosterUpdate>());

            FlushCoalescer(server);

            // The two deltas to the same element collapse into one send carrying the summed amount.
            var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkItemRosterUpdate>());
            Assert.Equal(5, sent.Amount);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                var update = Assert.Single(client.InternalMessages.GetMessages<UpdateItemRoster>());
                Assert.Equal(5, update.Amount);
            }
        }

        /// <summary>
        /// A clear drops the roster's pending updates, so no stale update rides out after the clear.
        /// </summary>
        [Fact]
        public void ServerClearDropsPendingItemRosterUpdates()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            var item = TestEnvironment.Server.CreateRegisteredObject<ItemObject>("item1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
                client.CreateRegisteredObject<ItemObject>("item1");
            }

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, new ItemRosterUpdated(itemRoster, item, null, 5));
            server.SimulateMessage(this, new ItemRosterCleared(itemRoster));

            FlushCoalescer(server);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClearItemRoster>());
                Assert.Equal(0, client.InternalMessages.GetMessageCount<UpdateItemRoster>());
            }
        }

        /// <summary>
        /// An update enqueued after a clear survives the drop and arrives after the clear.
        /// </summary>
        [Fact]
        public void ServerPostClearItemRosterUpdate_ArrivesAfterClear()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            var item = TestEnvironment.Server.CreateRegisteredObject<ItemObject>("item1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
                client.CreateRegisteredObject<ItemObject>("item1");
            }

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, new ItemRosterUpdated(itemRoster, item, null, 5));   // dropped by the clear
            server.SimulateMessage(this, new ItemRosterCleared(itemRoster));                  // sent immediately
            server.SimulateMessage(this, new ItemRosterUpdated(itemRoster, item, null, 3));   // survives the clear

            FlushCoalescer(server);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                var update = Assert.Single(client.InternalMessages.GetMessages<UpdateItemRoster>());
                Assert.Equal(3, update.Amount);

                var messages = client.InternalMessages.Messages;
                var clearIndex = messages.FindIndex(m => m is ClearItemRoster);
                var updateIndex = messages.FindIndex(m => m is UpdateItemRoster);

                Assert.True(clearIndex >= 0 && updateIndex >= 0);
                Assert.True(clearIndex < updateIndex, "post-clear update must arrive after the clear");
            }
        }

        // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
        // static scope so the merged send routes to clients.
        private static void FlushCoalescer(EnvironmentInstance server)
        {
            server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
        }
    }
}
