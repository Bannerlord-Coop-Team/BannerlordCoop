using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Settlements;

/// <summary>
/// Test the EnterSettlementAction LastVisitTimeOfOwner messages.
/// </summary>
public class SettlementEnterActionLastVisitTimeOfOwnerTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerSettlementEnterActionLastVisitTimeOfOwner_Publishes_AllClients()
    {
        var settlement = TestEnvironment.Server.CreateRegisteredObject<Settlement>("settlement1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Settlement>("settlement1");
        }

        var triggerMessage = new SettlementChangedLastVisitTimeOfOwner(settlement, 5f);

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
