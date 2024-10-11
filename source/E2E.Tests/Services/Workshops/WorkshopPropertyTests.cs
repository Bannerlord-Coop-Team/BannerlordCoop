using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Workshops
{
    public class WorkshopPropertyTests : IDisposable
    {
        E2ETestEnvironment TestEnvironement { get; }

        EnvironmentInstance Server => TestEnvironement.Server;
        IEnumerable<EnvironmentInstance> Clients => TestEnvironement.Clients;

        string WorkshopId { get; }
        string WorkshopTypeId { get; }
        string WorkshopTypeId2 { get; }

        public void Dispose()
        {
            TestEnvironement.Dispose();
        }

        public WorkshopPropertyTests(ITestOutputHelper output)
        {
            TestEnvironement = new E2ETestEnvironment(output);

            var workshop = GameObjectCreator.CreateInitializedObject<Workshop>();
            var workshopType = GameObjectCreator.CreateInitializedObject<WorkshopType>();
            var workshopType2 = GameObjectCreator.CreateInitializedObject<WorkshopType>();

            Server.ObjectManager.AddNewObject(workshopType, out string WorkshopTypeId);
            Server.ObjectManager.AddNewObject(workshopType2, out string WorkshopTypeId2);
            Server.ObjectManager.AddNewObject(workshop, out string WorkshopId);

            this.WorkshopId = WorkshopId;
            this.WorkshopTypeId = WorkshopTypeId;
            this.WorkshopTypeId2 = WorkshopTypeId2;

            foreach (var client in Clients)
            {
                var _workshop = GameObjectCreator.CreateInitializedObject<Workshop>();
                var _workshopType = GameObjectCreator.CreateInitializedObject<WorkshopType>();
                var _workshopType2 = GameObjectCreator.CreateInitializedObject<WorkshopType>();
                client.ObjectManager.AddExisting(this.WorkshopId, _workshop);
                client.ObjectManager.AddExisting(this.WorkshopTypeId, _workshopType);
                client.ObjectManager.AddExisting(this.WorkshopTypeId2, _workshopType2);
            }
        }

        [Fact]
        public void ServerChangeWorkshopCapital_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));

            // Act
            Server.Call(() =>
            {
                serverWorkshop.Capital = 5;
            });


            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(serverWorkshop.Capital, clientWorkshop.Capital);
            }
        }

        [Fact]
        public void ServerChangeWorkshopInitialCapital_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));

            // Act
            Server.Call(() =>
            {
                serverWorkshop.InitialCapital = 5;
            });


            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(serverWorkshop.InitialCapital, clientWorkshop.InitialCapital);
            }
        }

        [Fact]
        public void ServerChangeWorkshopLastRunCampaignTime_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));

            // Act
            Server.Call(() =>
            {
                serverWorkshop.LastRunCampaignTime = new CampaignTime(500);
            });

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(serverWorkshop.LastRunCampaignTime, clientWorkshop.LastRunCampaignTime);
            }
        }

        [Fact]
        public void ServerChangeWorkshopType_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            Assert.True(Server.ObjectManager.TryGetObject<WorkshopType>(WorkshopTypeId2, out var serverWorkshopType));
            // Act
            Server.Call(() =>
            {
                serverWorkshop.WorkshopType = serverWorkshopType;
            });

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(serverWorkshop.WorkshopType.StringId, clientWorkshop.WorkshopType.StringId);
            }
        }
        [Fact]
        public void ServerChangeWorkshopOwner_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironement.Server;

            var field = AccessTools.Field(typeof(Workshop), nameof(Workshop._owner));

            // Get field intercept to use on the server to simulate the field changing
            var intercept = TestEnvironement.GetIntercept(field);

            // Create hero instances on server
            var newOwner = ObjectHelper.SkipConstructor<Hero>();
            Assert.True(server.ObjectManager.AddNewObject(newOwner, out var newOwnerId));

            // Create hero instances on all clients
            foreach (var client in Clients)
            {
                var clientOwner = ObjectHelper.SkipConstructor<Hero>();
                Assert.True(client.ObjectManager.AddExisting(newOwnerId, clientOwner));
            }

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var workshop));
                Assert.True(server.ObjectManager.TryGetObject<Hero>(newOwnerId, out var owner));

                Assert.Null(workshop.Owner);

                // Simulate the field changing
                intercept.Invoke(null, new object[] { workshop, owner });

                Assert.Same(owner, workshop.Owner);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(newOwnerId, out var clientOwner));

                Assert.True(clientOwner == clientWorkshop.Owner);
            }
        }
        [Fact]
        public void ServerChangeWorkshopCustomName_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironement.Server;

            var field = AccessTools.Field(typeof(Workshop), nameof(Workshop._customName));

            // Get field intercept to use on the server to simulate the field changing
            var intercept = TestEnvironement.GetIntercept(field);

            // Create custom name instances on server
            TextObject newCustomName = new TextObject("New Workshop Name");

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var workshop));

                // Simulate the field changing
                intercept.Invoke(null, new object[] { workshop, newCustomName });

                Assert.Equal(newCustomName, workshop.Name);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(newCustomName.ToString(), clientWorkshop.Name.ToString());
            }
        }
        [Fact]
        public void ServerChangeWorkshopTag_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironement.Server;

            var field = AccessTools.Field(typeof(Workshop), nameof(Workshop._tag));

            // Get field intercept to use on the server to simulate the field changing
            var intercept = TestEnvironement.GetIntercept(field);

            // Create tag instance on server
            string newTag = "New Workshop Tag";

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var workshop));

                // Simulate the field changing
                intercept.Invoke(null, new object[] { workshop, newTag });

                Assert.Equal(newTag, workshop.Tag);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(newTag, clientWorkshop.Tag);
            }
        }
        [Fact]
        public void ServerChangeWorkshopSettlement_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironement.Server;

            var field = AccessTools.Field(typeof(Workshop), nameof(Workshop._settlement));

            // Get field intercept to use on the server to simulate the field changing
            var intercept = TestEnvironement.GetIntercept(field);

            // Create settlement instances on server
            var newSettlement = ObjectHelper.SkipConstructor<Settlement>();
            Assert.True(server.ObjectManager.AddNewObject(newSettlement, out var newSettlementId));

            // Create settlement instances on all clients
            foreach (var client in Clients)
            {
                var clientSettlement = ObjectHelper.SkipConstructor<Settlement>();
                Assert.True(client.ObjectManager.AddExisting(newSettlementId, clientSettlement));
            }

            // Act
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var workshop));
                Assert.True(server.ObjectManager.TryGetObject<Settlement>(newSettlementId, out var settlement));

                Assert.Null(workshop.Settlement);  // Before changing, verify the workshop has no settlement

                // Simulate the field changing
                intercept.Invoke(null, new object[] { workshop, settlement });

                Assert.Same(settlement, workshop.Settlement);
            });

            // Assert
            foreach (var client in Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(newSettlementId, out var clientSettlement));

                Assert.True(clientSettlement == clientWorkshop.Settlement);
            }
        }
    }
}