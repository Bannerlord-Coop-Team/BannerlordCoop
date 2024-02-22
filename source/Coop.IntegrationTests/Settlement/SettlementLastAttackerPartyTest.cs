using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

namespace Coop.IntegrationTests.Settlement;

/// <summary>
/// Used to test that the LastAttackerParty is sent to client.
/// </summary>
public class SettlementLastAttackerPartyTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerLastAttackerPartyChanged_Publishes_AllClients()
    {
        string settlementId = "Settlement1";
        string mobilePartyId = "MobileParty1";

        var triggerMessage = new SettlementChangedLastAttackerParty(settlementId, mobilePartyId);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementLastAttackerParty>());

        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementLastAttackerParty>());
        }
    }
}
