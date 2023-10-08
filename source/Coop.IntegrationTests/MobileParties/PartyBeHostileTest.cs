using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.IntegrationTests.MobileParties
{
    public class PartyBeHostileTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public PartyBeHostileTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending LocalBecomeHostile on one client
        /// Triggers PartyBeHostile on all other clients
        /// </summary>
        [Fact]
        public void BeHostile_Publishes_AllClients()
        {
            // Arrange
            var attackId = "atk";
            var defenderId = "def";

            var message = new LocalBecomeHostile(attackId, defenderId, 50);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<PartyBeHostile>());
            }
        }
    }
}