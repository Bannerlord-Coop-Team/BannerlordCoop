using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.VillageMarketDatas
{
    public class VillageMarketDataSyncTest : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string VillageId;
        private readonly string Village2Id;
        private readonly string VillageMarketDataId;

        public VillageMarketDataSyncTest(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var village = GameObjectCreator.CreateInitializedObject<Village>();
            var village2 = GameObjectCreator.CreateInitializedObject<Village>();
            VillageMarketData villageMarketData = new VillageMarketData(village);

            // Create objects on the server
            Assert.True(Server.ObjectManager.AddNewObject(village, out VillageId));
            Assert.True(Server.ObjectManager.AddNewObject(village2, out Village2Id));
            Assert.True(Server.ObjectManager.AddNewObject(villageMarketData, out VillageMarketDataId));

            // Create objects on all clients
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.AddExisting(VillageId, village));
                Assert.True(client.ObjectManager.AddExisting(Village2Id, village2));
                Assert.True(client.ObjectManager.AddExisting(VillageMarketDataId, villageMarketData));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Building_Sync()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var villageField = AccessTools.Field(typeof(VillageMarketData), nameof(VillageMarketData._village));

            // Get field intercept to use on the server to simulate the field changing
            var villageIntercept = TestEnvironment.GetIntercept(villageField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<VillageMarketData>(VillageMarketDataId, out var serverVillageMarketData));
                Assert.True(server.ObjectManager.TryGetObject<Village>(Village2Id, out var newVillage));

                // Simulate the field changing
                villageIntercept.Invoke(null, new object[] { serverVillageMarketData, newVillage });

                Assert.Equal(newVillage, serverVillageMarketData._village);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<VillageMarketData>(VillageMarketDataId, out var clientVillageMarketData));
                Assert.True(client.ObjectManager.TryGetObject<Village>(Village2Id, out var newVillage));

                Assert.Equal(newVillage, clientVillageMarketData._village);
            }
        }
    }
}
