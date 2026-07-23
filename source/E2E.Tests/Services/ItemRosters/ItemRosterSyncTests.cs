using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemRosters
{
    public class ItemRosterSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string ItemRosterId;

        public ItemRosterSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var itemRoster = new ItemRoster();

            // Create item roster on the server
            Assert.True(Server.ObjectManager.AddNewObject(itemRoster, out ItemRosterId));

            // Create item roster on all clients
            foreach (var client in Clients)
            {
                var client_itemRoster = new ItemRoster();
                Assert.True(client.ObjectManager.AddExisting(ItemRosterId, client_itemRoster));
            }
        }

        [Fact(Skip = "_count sync is disabled in ItemRosterSync.cs")]
        public void Server_ItemRoster__count()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var field = AccessTools.Field(typeof(ItemRoster), nameof(ItemRoster._count));

            // Get field intercept to use on the server to simulate the field changing
            var intercept = TestEnvironment.GetIntercept(field);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var itemRoster));
                Assert.Equal(0, itemRoster._count);

                // Simulate the field changing
                intercept.Invoke(null, new object[] { itemRoster, 5 });

                Assert.Equal(5, itemRoster._count);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<ItemRoster>(ItemRosterId, out var itemRoster));
                Assert.Equal(5, itemRoster._count);
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }
    }
}
