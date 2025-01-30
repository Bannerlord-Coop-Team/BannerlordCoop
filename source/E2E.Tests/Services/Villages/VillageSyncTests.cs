using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages
{
    public class VillageSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }

        EnvironmentInstance Server => TestEnvironment.Server;

        IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;

        private readonly string VillageId;
        private readonly string VpcId;
        private readonly string VillageMarketDataId;
        private readonly string SettlementId;
        private readonly string VillageTypeId;

        public VillageSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var village = GameObjectCreator.CreateInitializedObject<Village>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            VillagerPartyComponent vpc = new VillagerPartyComponent(village);
            VillageMarketData villageMarketData = new VillageMarketData(village);
            VillageType villageType = new VillageType("test");

            // Create objects on the server
            Assert.True(Server.ObjectManager.AddNewObject(village, out VillageId));
            Assert.True(Server.ObjectManager.AddNewObject(vpc, out VpcId));
            Assert.True(Server.ObjectManager.AddNewObject(villageMarketData, out VillageMarketDataId));
            Assert.True(Server.ObjectManager.AddNewObject(settlement, out SettlementId));
            Assert.True(Server.ObjectManager.AddNewObject(villageType, out VillageTypeId));

            // Create objects on all clients
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.AddExisting(VillageId, village));
                Assert.True(client.ObjectManager.AddExisting(VpcId, vpc));
                Assert.True(client.ObjectManager.AddExisting(VillageMarketDataId, villageMarketData));
                Assert.True(client.ObjectManager.AddExisting(SettlementId, settlement));
                Assert.True(client.ObjectManager.AddExisting(VillageTypeId, villageType));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void Server_Village_Sync()
        {
            // Arrange
            var server = TestEnvironment.Server;

            var villagerPartyField = AccessTools.Field(typeof(Village), nameof(Village.VillagerPartyComponent));
            var villageTypeField = AccessTools.Field(typeof(Village), nameof(Village.VillageType));
            var boundField = AccessTools.Field(typeof(Village), nameof(Village._bound));
            var marketDataField = AccessTools.Field(typeof(Village), nameof(Village._marketData));
            var tradeBoundField = AccessTools.Field(typeof(Village), nameof(Village._tradeBound));
            var villageStateField = AccessTools.Field(typeof(Village), nameof(Village._villageState));

            // Get field intercept to use on the server to simulate the field changing
            var villagerPartyIntercept = TestEnvironment.GetIntercept(villagerPartyField);
            var villageTypeIntercept = TestEnvironment.GetIntercept(villageTypeField);
            var boundIntercept = TestEnvironment.GetIntercept(boundField);
            var marketDataIntercept = TestEnvironment.GetIntercept(marketDataField);
            var tradeBoundIntercept = TestEnvironment.GetIntercept(tradeBoundField);
            var villageStateIntercept = TestEnvironment.GetIntercept(villageStateField);

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Settlement>(SettlementId, out var serverSettlement));
                Assert.True(server.ObjectManager.TryGetObject<VillageType>(VillageTypeId, out var serverVillageType));
                Assert.True(server.ObjectManager.TryGetObject<VillageMarketData>(VillageMarketDataId, out var serverVillageMarketData));
                Assert.True(server.ObjectManager.TryGetObject<Village>(VillageId, out var serverVillage));
                Assert.True(server.ObjectManager.TryGetObject<VillagerPartyComponent>(VpcId, out var serverVpc));

                // Simulate the field changing
                villagerPartyIntercept.Invoke(null, new object[] { serverVillage, serverVpc });
                villageTypeIntercept.Invoke(null, new object[] { serverVillage, serverVillageType });
                boundIntercept.Invoke(null, new object[] { serverVillage, serverSettlement });
                marketDataIntercept.Invoke(null, new object[] { serverVillage, serverVillageMarketData });
                tradeBoundIntercept.Invoke(null, new object[] { serverVillage, serverSettlement });
                villageStateIntercept.Invoke(null, new object[] { serverVillage, Village.VillageStates.Looted });

                Assert.Equal(serverVpc, serverVillage.VillagerPartyComponent);
                Assert.Equal(serverSettlement, serverVillage._bound);
                Assert.Equal(serverVillageType, serverVillage.VillageType);
                Assert.Equal(serverVillageMarketData, serverVillage._marketData);
                Assert.Equal(serverSettlement, serverVillage._tradeBound);
                Assert.Equal(Village.VillageStates.Looted, serverVillage._villageState);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(SettlementId, out var clientSettlement));
                Assert.True(client.ObjectManager.TryGetObject<VillageType>(VillageTypeId, out var clientVillageType));
                Assert.True(client.ObjectManager.TryGetObject<VillageMarketData>(VillageMarketDataId, out var clientVillageMarketData));
                Assert.True(client.ObjectManager.TryGetObject<Village>(VillageId, out var clientVillage));
                Assert.True(client.ObjectManager.TryGetObject<VillagerPartyComponent>(VpcId, out var clientVpc));

                Assert.Equal(clientVpc, clientVillage.VillagerPartyComponent);
                Assert.Equal(clientSettlement, clientVillage._bound);
                Assert.Equal(clientVillageType, clientVillage.VillageType);
                Assert.Equal(clientVillageMarketData, clientVillage._marketData);
                Assert.Equal(clientSettlement, clientVillage._tradeBound);
                Assert.Equal(Village.VillageStates.Looted, clientVillage._villageState);
            }
        }
    }
}
