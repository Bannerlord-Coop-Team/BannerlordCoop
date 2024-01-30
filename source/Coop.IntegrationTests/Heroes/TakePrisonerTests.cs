using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Heroes.Messages;

namespace Coop.IntegrationTests.Heroes
{
    public class TakePrisonerTests
    {
        internal TestEnvironment TestEnvironment { get; }

        public TakePrisonerTests()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending PrisonerTaken on one client
        /// Triggers TakePrisoner on all other clients
        /// </summary>
        [Fact]
        public void PrisonerTaken_Publishes_AllClients()
        {
            // Arrange
            var partyId = "party1";
            var charaterId = "character1";

            var message = new PrisonerTaken(partyId, charaterId, true);

            var client1 = TestEnvironment.Clients.First();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, message);

            // Assert
            Assert.Equal(1, server.InternalMessages.GetMessageCount<TakePrisoner>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<TakePrisoner>());
            }
        }
    }
}
