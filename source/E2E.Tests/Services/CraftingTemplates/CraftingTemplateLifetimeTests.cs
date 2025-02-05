using E2E.Tests.Environment;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CraftingTemplates
{
    public class CraftingTemplateLifetimeTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public CraftingTemplateLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCraftingTemplate_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? templateId = null;

            server.Call(() =>
            {
                CraftingTemplate craftingTemplate = new CraftingTemplate();

                Assert.True(server.ObjectManager.TryGetId(craftingTemplate, out templateId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<CraftingTemplate>(templateId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<CraftingTemplate>(templateId, out var _));
            }
        }

        [Fact]
        public void ClientCreateCraftingTemplate_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            // Act
            string? templateId = null;
            client1.Call(() =>
            {
                var template = new CraftingTemplate();

                Assert.False(client1.ObjectManager.TryGetId(template, out templateId));
            });

            // Assert
            Assert.Null(templateId);
        }
    }
}
