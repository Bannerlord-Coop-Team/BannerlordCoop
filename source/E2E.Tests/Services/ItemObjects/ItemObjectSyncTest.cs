using Common.Util;
using E2E.Tests.Environment;
using HarmonyLib;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.ItemObjectService
{
    public class ItemObjectSyncTest : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public ItemObjectSyncTest(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateItemObject_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? itemObjectId = null;

            var typeField = AccessTools.Field(typeof(ItemObject), nameof(ItemObject.Type));

            var typeIntercept = TestEnvironment.GetIntercept(typeField);
            

            server.Call(() =>
            {
                ItemObject itemObject = new ItemObject();
                Assert.True(server.ObjectManager.TryGetId(itemObject, out itemObjectId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(itemObjectId, out ItemObject itemObject));

            server.Call(() =>
            {
                typeIntercept.Invoke(null, new object[] { itemObject, 69 });
            });

            server.ObjectManager.TryGetId(itemObject, out string serverObjectId);

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(serverObjectId, out ItemObject clientItemObject));
                Assert.Equal(itemObject.Type, clientItemObject.Type);

                client.ObjectManager.TryGetId(clientItemObject, out string clientObjectId);

                Assert.Equal(serverObjectId, clientObjectId);
            }
        }
    }
}
