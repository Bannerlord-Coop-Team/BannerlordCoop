using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement;

/// <summary>
/// Test the EnterSettlementAction LastVisitTimeOfOwner messages.
/// </summary>
public class SettlementEnterActionLastVisitTimeOfOwnerTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerSettlementEnterActionLastVisitTimeOfOwner_Publishes_AllClients()
    {
        string SettlementID = "Settlement1";
        float currentTime = 4329313.0f;

        var triggerMessage = new SettlementChangedLastVisitTimeOfOwner(SettlementID, currentTime);

        var server = TestEnvironment.Server;
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeLastVisitTimeOfOwner>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementLastVisitTimeOfOwner>());
        }
    }
}
