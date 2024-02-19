using Common.Util;
using Coop.Core.Client.Services.Armies.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages.Lifetime;
using System.Runtime.Remoting;


namespace Coop.IntegrationTests.Armies
{
    public class DestroyArmyTest
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
            var triggerMessage = ObjectHelper.SkipConstructor<ArmyDestroyed>();

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkDestroyArmy>());

            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<DestroyArmy>());
            }
        }
    }
}
