using Common.Util;
using Coop.Core.Client.Services.Armies.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Messages;


namespace Coop.IntegrationTests.Armies
{
    public class CreateArmyTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives RemoveMobilePartyInArmy messages.
        /// </summary>
        [Fact]
        public void ServerMobilePartyInArmyRemoved_Publishes_AllClients()
        {
            // Arrange
            var triggerMessage = ObjectHelper.SkipConstructor<ArmyCreated>();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkCreateArmy>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<CreateArmy>());
            }
        }
    }
}
