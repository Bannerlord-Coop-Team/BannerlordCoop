using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

        string WorkshopId { get; set; }
        string WorkshopTypeId { get; set; }

        public void Dispose()
        {
            TestEnvironement.Dispose();
        }

        public WorkshopPropertyTests(ITestOutputHelper output)
        {
            TestEnvironement = new E2ETestEnvironment(output);

            var workshop = GameObjectCreator.CreateInitializedObject<Workshop>();
            var workshopType = GameObjectCreator.CreateInitializedObject<WorkshopType>();

            Server.ObjectManager.AddNewObject(workshopType, out string typeId);
            Server.ObjectManager.AddNewObject(workshop, out string workshopId);

            WorkshopId = workshopId;
            WorkshopTypeId = typeId;

            foreach (var client in Clients)
            {
                var _workshop = GameObjectCreator.CreateInitializedObject<Workshop>();
                var _workshopType = GameObjectCreator.CreateInitializedObject<WorkshopType>();
                client.ObjectManager.AddExisting(WorkshopId, _workshop);
                client.ObjectManager.AddExisting(WorkshopTypeId, _workshopType);
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

        //This one does nothing atm.... Campaign.Current.Workshops is empty??
        [Fact]
        public void ServerChangeWorkshopType_SyncAllClients()
        {
            Assert.True(Server.ObjectManager.TryGetObject<Workshop>(WorkshopId, out var serverWorkshop));
            Assert.True(Server.ObjectManager.TryGetObject<WorkshopType>(WorkshopTypeId, out var serverWorkshopType));
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
    }
}
