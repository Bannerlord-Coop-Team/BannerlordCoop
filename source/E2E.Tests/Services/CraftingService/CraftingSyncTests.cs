using E2E.Tests.Environment;
using HarmonyLib;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CraftingService
{
    public class CraftingSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public CraftingSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCrafting_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? craftingId = null;

            var currentHistoryIndexField = AccessTools.Field(typeof(Crafting), nameof(Crafting._currentHistoryIndex));
            var craftedItemObjectField = AccessTools.Field(typeof(Crafting), nameof(Crafting._craftedItemObject));

            var currentHistoryIntercept = TestEnvironment.GetIntercept(currentHistoryIndexField);
            var craftingItemObjectIntercept = TestEnvironment.GetIntercept(craftedItemObjectField);
            

            server.Call(() =>
            {
                CraftingTemplate craftingTemplate = new CraftingTemplate();
                CultureObject cultureObject = new CultureObject();
                Crafting crafting = new Crafting(craftingTemplate, cultureObject, new TextObject("test"));
                

                Assert.True(server.ObjectManager.TryGetId(crafting, out craftingId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(craftingId, out Crafting serverCrafting));

            server.Call(() =>
            {
                currentHistoryIntercept.Invoke(null, new object[] { serverCrafting, 69 });
                craftingItemObjectIntercept.Invoke(null, new object[] { serverCrafting, new ItemObject("need lifetime sync") });
            });

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(craftingId, out Crafting clientCrafting));
                Assert.Equal(serverCrafting._currentHistoryIndex, clientCrafting._currentHistoryIndex);
                Assert.Equal(serverCrafting._craftedItemObject, clientCrafting._craftedItemObject);

            }
        }
    }
}
