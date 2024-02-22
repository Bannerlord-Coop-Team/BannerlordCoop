using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement;
public class SettlementHitPointsTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Used to Test that client recieves SettlementHitPoints messsages.
    /// </summary>
    [Fact]
    public void ServerSettlementHitPointsChanged_Publishes_AllClients()
    {
        // Arrange
        string settlementId = "Settlement1";
        float hitPoints = 99.5f;
        var triggerMessage = new SettlementChangedSettlementHitPoints(settlementId, hitPoints);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementHitPoints>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementHitPoints>());
        }
    }
}
