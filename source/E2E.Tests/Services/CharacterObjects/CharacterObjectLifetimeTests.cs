using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CharacterObjects
{
    public class CharacterObjectLifetimeTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        public CharacterObjectLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCharacterObject_SyncAllClients()
        { 
            // Arrange
            var server = TestEnvironment.Server;

            // Act
            string? characterId = null;
            server.Call(() =>
            {
                var characterObject = new CharacterObject();
                Assert.True(server.ObjectManager.TryGetId(characterObject, out characterId));
            });

            // Assert
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(characterId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var _));
            }
        }


        [Fact]
        public void ClientCreateCharacterObject_DoesNothing()
        {
            // Arrange
            var client1 = TestEnvironment.Clients.First();
            var server = TestEnvironment.Server;

            // Act
            string? characterId = null;
            client1.Call(() =>
            {
                var characterObject = new CharacterObject();
                Assert.False(client1.ObjectManager.TryGetId(characterObject, out characterId));
            });

            // Assert
            Assert.False(server.ObjectManager.TryGetObject<CharacterObject>(characterId, out var _));

            foreach (var client in TestEnvironment.Clients)
            {
                Assert.False(client.ObjectManager.TryGetObject<CharacterObject>(characterId, out var _));
            }
        }
    }
}
