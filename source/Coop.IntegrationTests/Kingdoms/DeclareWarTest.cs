using Coop.IntegrationTests.Environment;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.IntegrationTests.Kingdoms
{
    public class DeclareWarTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public DeclareWarTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending DeclareWar on one client
        /// Triggers WarDeclared on all other clients
        /// </summary>
        [Fact]
        public void BeHostile_Publishes_AllClients()
        {
            // Arrange
            var attackId = "atk";
            var defenderId = "def";

            var message = new DeclareWar(attackId, defenderId, 50);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<WarDeclared>());
            }
        }
    }
}