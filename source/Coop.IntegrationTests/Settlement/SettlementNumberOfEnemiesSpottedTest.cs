using Coop.Core.Server.Services.Settlements.Messages;
using Coop.Core.Server.Services.Villages.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;
using GameInterface.Services.Villages.Messages;

namespace Coop.IntegrationTests.Settlement;
public class SettlementNumberOfEnemiesSpottedTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Used to Test that client recieves NumberOfEnemiesSpottedAround messsages.
    /// </summary>
    [Fact]
    public void ServerVillageStateChanged_Publishes_AllClients()
    {
        // Arrange
        string settlementId = "Settlement1";
        float NumberOfEnemiesSpottedAround = 15.4f;
        var triggerMessage = new SettlementChangedEnemiesSpotted(settlementId, NumberOfEnemiesSpottedAround);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementEnemiesSpotted>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementEnemiesSpotted>());
        }
    }
}
