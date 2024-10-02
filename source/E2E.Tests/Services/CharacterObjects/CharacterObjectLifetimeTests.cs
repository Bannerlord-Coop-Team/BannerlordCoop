using E2E.Tests.Environment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.CharacterObjects
{
    public class CharacterObjectLifetimeTests
    {
        E2ETestEnvironment TestEnvironment { get; }

        private readonly string CharacterObjectId;

        public CharacterObjectLifetimeTests(ITestOutputHelper output)
        {
            TestEnvironment = new E2ETestEnvironment(output);

            var characterObject = new CharacterObject();

            //Create object on server
            Assert.True(TestEnvironment.Server.ObjectManager.AddNewObject(characterObject, out CharacterObjectId));

            // Create object on all clients
            foreach (var client in TestEnvironment.Clients)
            {
                var client_characterObject = new CharacterObject();
                Assert.True(client.ObjectManager.AddExisting(CharacterObjectId, client_characterObject));
            }
        }

        public void Dispose()
        {
            TestEnvironment.Dispose();
        }

        [Fact]
        public void ServerCreateCharacterObject_SyncAllClients()
        {
            var server = TestEnvironment.Server;

            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(CharacterObjectId, out var _));            
            });

            foreach(var client in TestEnvironment.Clients)
            {
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(CharacterObjectId, out var _));
            }
        }


        [Fact]
        public void ClientCreateCharacterObejct_DoesNothing()
        {
            var client1 = TestEnvironment.Clients.First();

            client1.Call(() =>
            {
                CharacterObject character = new CharacterObject();
                Assert.False(client1.ObjectManager.TryGetId(character, out var _));
            });
        }
    }
}
