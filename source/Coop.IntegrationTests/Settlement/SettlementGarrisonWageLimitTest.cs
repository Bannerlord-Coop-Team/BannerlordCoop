using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement;

/// <summary>
/// Test the garrison wage limit
/// </summary>
public class SettlementGarrisonWageLimitTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Used to test that the client recieves Cache changes
    /// </summary>
    [Fact]
    public void ServerSettlementGarrisonWageLimitChanged_Publishes_AllClients()
    {
        // Arrange
        string settlementId = "Settlement1";
        int wageLimit = 45;
        var triggerMessage = new SettlementChangedGarrisonWageLimit(settlementId, wageLimit);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementGarrisonWagePaymentLimit>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementGarrisonWagePaymentLimit>());
        }
    }

}
