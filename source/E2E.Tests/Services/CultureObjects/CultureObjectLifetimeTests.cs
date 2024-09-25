using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CultureObjects
{
    public class CultureObjectLifetimeTests : IDisposable
    {
        E2ETestEnvironment TestEnvironment { get; }
        public CultureObjectLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCultureObject_SyncAllClients()
        {
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? cultureId = null;
            CultureObject? culture = null;

            server.Call(() =>
            {
                CultureObject cultureObject = new CultureObject();

                cultureId = cultureObject.StringId;

                Assert.True(server.ObjectManager.TryGetObject(cultureId, out CultureObject culture));
            });

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<CultureObject>(cultureId, out var _));
            }
        }

        [Fact]
        public void ClientCreateCultureObject_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();

            // Act
            string? cultureId = null;
            client1.Call(() =>
            {
                var cultureObject = new CultureObject();

                Assert.False(client1.ObjectManager.TryGetId(cultureObject, out cultureId));
            });

            // Assert
            Assert.Null(cultureId);
        }
    }
}
