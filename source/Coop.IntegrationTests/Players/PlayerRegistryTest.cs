using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Players.Messages;

namespace Coop.IntegrationTests.Players
{
    public class PlayerRegistryTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public PlayerRegistryTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending StartSettlementEncounterAttempted on one client
        /// Triggers PartyEnterSettlement on all other clients
        /// </summary>
        [Fact]
        public void ServerNewPlayerRegistered_ClientPublishes_RegisterPlayer()
        {
            Player player = new Player(Array.Empty<Byte>(),"Hero-CoopParty", "CoopParty", "characterObjectID", "ClanStringID");
            
     
            var message = new PlayerRegistered(player);

            var server1 = TestEnvironment.Server;

            server1.ReceiveMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<RegisterPlayer>());
            }
        }
    }
}
