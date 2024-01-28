using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;

namespace Coop.IntegrationTests.Towns
{
    public class TownGarrisonAutoRecruitmentIsEnabledTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves TownGarrisonAutoRecruitmentIsEnabledChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownGarrisonAutoRecruitmentIsEnabledChanged_Publishes_AllClients()
        {
            // Arrange
            string townId = "Settlement1";
            bool garrisonAutoRecruitmentIsEnabled = false;
            var triggerMessage = new TownGarrisonAutoRecruitmentIsEnabledChanged(townId, garrisonAutoRecruitmentIsEnabled);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownGarrisonAutoRecruitmentIsEnabled>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownGarrisonAutoRecruitmentIsEnabled>());
            }
        }
    }
}
