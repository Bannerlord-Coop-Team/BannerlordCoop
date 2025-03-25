using E2E.Tests.Util;
using GameInterface.Services.Heroes.Patches;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Towns
{
    public class TownCollectionTests : SyncTestBase
    {
        private readonly string townId;
        private readonly string villageId;
        private readonly string buildingId;
        private readonly string workshopId;

        public TownCollectionTests(ITestOutputHelper output) : base(output)
        {
            townId = TestEnvironment.CreateRegisteredObject<Town>();
            villageId = TestEnvironment.CreateRegisteredObject<Village>();
            buildingId = TestEnvironment.CreateRegisteredObject<Building>();
            workshopId = TestEnvironment.CreateRegisteredObject<Workshop>();
        }

        [Fact]
        private void ServerBuildingsAddedAndRemoved_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Building>(buildingId, out var building));

            //Act
            Server.Call(() =>
            {
                Assert.Contains(building, town.Buildings);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Building>(buildingId, out var clientBuilding));
                Assert.Contains(clientBuilding, clientTown.Buildings);
            }

            Server.Call(() =>
            {
                Assert.DoesNotContain(building, town.Buildings);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Building>(buildingId, out var clientBuilding));
                Assert.DoesNotContain(clientBuilding, clientTown.Buildings);
            }
        }

        [Fact]
        private void ServerTradeBoundVillagesCacheAddedAndRemoved_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));

            //Act
            Server.Call(() =>
            {
                Assert.Contains(village, town._tradeBoundVillagesCache);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Village>(villageId, out var clientVillage));
                Assert.Contains(clientVillage, clientTown._tradeBoundVillagesCache);
            }

            Server.Call(() =>
            {
                Assert.DoesNotContain(village, town._tradeBoundVillagesCache);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Village>(villageId, out var clientVillage));
                Assert.DoesNotContain(clientVillage, clientTown._tradeBoundVillagesCache);
            }
        }

        [Fact]
        private void ServerWorkshopsSetAndChanged_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(workshopId, out var workshop));
            //Act
            Server.Call(() =>
            {
                var workshops = new Workshop[20];
                workshops[12] = workshop;

                Assert.Equal(20, town.Workshops.Length);
                Assert.Equal(workshop, town.Workshops[12]);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(workshopId, out var clientWorkshop));
                Assert.Equal(20, clientTown.Workshops.Length);
                Assert.Equal(clientWorkshop, clientTown.Workshops[12]);
            }

            Server.Call(() =>
            {
                Assert.Equal(workshop, town.Workshops[5]);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(workshopId, out var clientWorkshop));
                Assert.Equal(clientWorkshop, clientTown.Workshops[5]);
            }
        }

        [Fact]
        private void ServerBuildingsInProgressSetAndChanged_SyncAllClients()
        {
            //Arrange
            Assert.True(Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(Server.ObjectManager.TryGetObject<Building>(buildingId, out var building));
            var secondBuildingId = TestEnvironment.CreateRegisteredObject<Building>();
            Assert.True(Server.ObjectManager.TryGetObject<Building>(secondBuildingId, out var secondBuilding));
            Server.Call(() =>
            {
                Assert.Empty(town.BuildingsInProgress);
                var newQueue = new Queue<Building>();
                newQueue.Enqueue(secondBuilding);

                Assert.Equal(secondBuilding, town.BuildingsInProgress.Peek());
                Assert.Single(town.BuildingsInProgress);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Building>(secondBuildingId, out var clientBuilding));
                Assert.Equal(clientBuilding, clientTown.BuildingsInProgress.Peek());
                Assert.Single(clientTown.BuildingsInProgress);
            }

            //Act
            Server.Call(() =>
            {
                Assert.Equal(2, town.BuildingsInProgress.Count);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.Equal(2, clientTown.BuildingsInProgress.Count);
            }

            Server.Call(() =>
            {
                Assert.Single(town.BuildingsInProgress);
            });

            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Town>(townId, out var clientTown));
                Assert.True(client.ObjectManager.TryGetObject<Building>(buildingId, out var clientBuilding));
                Assert.Equal(clientBuilding, clientTown.BuildingsInProgress.Peek());
                Assert.Single(town.BuildingsInProgress);
            }
        }
    }
}