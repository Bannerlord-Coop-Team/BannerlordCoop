using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
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
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            var newOwner = GameObjectCreator.CreateInitializedObject<Hero>();

            // Act
            Server.Call(() =>
            {
                serverWorkshop.ChangeOwnerOfWorkshop(newOwner, serverWorkshop.WorkshopType, serverWorkshop.Capital);
            });

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(newOwner.StringId, clientWorkshop.Owner.StringId);
            }
        }
        [Fact]
        public void ServerChangeWorkshopCustomName_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            var newCustomName = new TextObject("New Workshop Name");

            // Act
            Server.Call(() =>
            {
                serverWorkshop.SetCustomName(newCustomName);
            });

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(newCustomName.ToString(), clientWorkshop._customName.ToString());
            }
        }

        [Fact]
        public void WorkshopTag_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            var originalTag = serverWorkshop.Tag;

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(originalTag, clientWorkshop.Tag);  // Ensure client has the correct tag
            }
        }

        [Fact]
        public void WorkshopSettlement_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            var originalSettlement = serverWorkshop.Settlement;

            // Assert
            foreach (var client in TestEnvironement.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var clientWorkshop));
                Assert.Equal(originalSettlement.StringId, clientWorkshop.Settlement.StringId);  // Ensure client has the correct settlement
            }
        }


    }
}
