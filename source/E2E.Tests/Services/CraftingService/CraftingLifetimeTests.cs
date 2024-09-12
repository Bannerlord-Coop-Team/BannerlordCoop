using E2E.Tests.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CraftingService
{
    public class CraftingLifetimeTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public CraftingLifetimeTests(ITestOutputHelper output)
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
            string? craftingTemplateId = null;
            string? basicCultureObjectId = null;

            server.Call(() =>
            {
                CraftingTemplate craftingTemplate = new CraftingTemplate();
                CultureObject cultureObject = new CultureObject();
                Crafting crafting = new Crafting(craftingTemplate, cultureObject, new TextObject("test"));
                
                Assert.True(server.ObjectManager.TryGetId(crafting, out string foundCraftingId));
                craftingId = foundCraftingId;
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<Crafting>(craftingId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<Crafting>(craftingId, out var _));
            }
        }

        [Fact]
        public void ClientCreateCrafting_DoesNothing()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? clanId = null;
            TestEnvironment.Clients.First().Call(() =>
            {
                var clan = Clan.CreateClan("");
                clanId = clan.StringId;
            });

            // Assert
            Assert.False(server.ObjectManager.TryGetObject<Clan>(clanId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.False(client.ObjectManager.TryGetObject<Clan>(clanId, out var _));
            }
        }
    }
}
