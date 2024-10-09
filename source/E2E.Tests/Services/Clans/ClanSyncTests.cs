using E2E.Tests.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans
{
    public class ClanSyncTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public ClanSyncTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerUpdateClan_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? clanId = null;
            server.Call(() =>
            {
                var clan = Clan.CreateClan("");
                clanId = clan.StringId;
                clan.Name = new TextObject("testName");
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject(clanId, out Clan serverClan));


            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject(clanId, out Clan clientClan));
                Assert.Equal(serverClan.Name, clientClan.Name);
            }
        }
    }
}
