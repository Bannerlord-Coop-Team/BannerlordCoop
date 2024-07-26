using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlements;
/// <summary>
/// Test NotableCaches function call test
/// </summary>
public class SettlementCollectNotablesToCacheTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();
    
    /// <summary>
    /// Test that the cache is published to all clients.
    /// </summary>
    [Fact]
    public void ServerSettlementNotableCache_Publishes_AllClients()
    {
        // Arrange
        string settlementId = "Settlement1";
        var cacheNotables = new List<string> { "test1", "test2", "test3" };
        var triggerMessage = new SettlementChangedNotablesCache(settlementId, cacheNotables);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementNotablesCache>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementNotablesCache>());
        }
    }
}
