using Coop.IntegrationTests.Environment;
using GameInterface.Services.Settlements.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment.Instance;

namespace Coop.IntegrationTests.Settlements;

/// <summary>
/// Handles Settlement.CanBeClaimed Sync Tests
/// </summary>
public class SettlementClaimantCampaignBehaviorCanBeClaimedTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void SettlementClaimantCampaignBehaviorCanBeClaimed_Publishes_AllClients()
    {
        string SettlementId = "SettlementID";
        int CanBeClaimed = 10;

        var triggerMessage = new SettlementClaimantCanBeClaimedChanged(SettlementId, CanBeClaimed);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementClaimantCanBeClaimed>());
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementClaimantCanBeClaimed>());
        }



    }

}
