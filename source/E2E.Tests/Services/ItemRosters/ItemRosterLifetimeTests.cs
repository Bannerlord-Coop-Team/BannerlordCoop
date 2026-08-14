using E2E.Tests.Environment;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Messages;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemRosters
{
    public class ItemRosterLifetimeTests : IDisposable
    {
        private E2ETestEnvironment TestEnvironment { get; }

        public ItemRosterLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateStandaloneItemRoster_DoesNotSync()
        {
            var server = TestEnvironment.Server;
            server.InternalMessages.Clear();
            server.NetworkSentMessages.Clear();

            string? itemRosterId = null;
            server.Call(() =>
            {
                var itemRoster = new ItemRoster();

                Assert.False(server.ObjectManager.TryGetId(itemRoster, out itemRosterId));
            });

            Assert.Null(itemRosterId);
            Assert.Empty(server.InternalMessages.GetMessages<ItemRosterCreated>());
            Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkCreateItemRoster>());
        }

        [Fact]
        public void ServerMutateStandaloneItemRoster_DoesNotSync()
        {
            var server = TestEnvironment.Server;
            var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            server.InternalMessages.Clear();
            server.NetworkSentMessages.Clear();

            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                var itemRoster = new ItemRoster();

                itemRoster.AddToCounts(new EquipmentElement(item), 2);
                itemRoster.Clear();

                Assert.False(server.ObjectManager.TryGetId(itemRoster, out _));
                Assert.Empty(itemRoster);
            });

            Assert.Empty(server.InternalMessages.GetMessages<ItemRosterUpdated>());
            Assert.Empty(server.InternalMessages.GetMessages<ItemRosterCleared>());
            Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkCreateItemRoster>());
            Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkItemRosterUpdate>());
            Assert.Empty(server.NetworkSentMessages.GetMessages<NetworkItemRosterClear>());
        }

        [Fact]
        public void ClientCreateAndMutateStandaloneItemRoster_StaysLocal()
        {
            var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
            var client = TestEnvironment.Clients.First();
            client.InternalMessages.Clear();
            client.NetworkSentMessages.Clear();

            string? rosterId = null;
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                var itemRoster = new ItemRoster();

                itemRoster.AddToCounts(new EquipmentElement(item), 2);
                Assert.Single(itemRoster);

                itemRoster.Clear();

                Assert.False(client.ObjectManager.TryGetId(itemRoster, out rosterId));
                Assert.Empty(itemRoster);
            });

            Assert.Null(rosterId);
            Assert.Empty(client.InternalMessages.GetMessages<ItemRosterUpdated>());
            Assert.Empty(client.InternalMessages.GetMessages<ItemRosterCleared>());
            Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCreateItemRoster>());
        }
    }
}
