using Coop.Core.Server.Services.Settlements.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Settlements.Messages;

using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Settlements;


/// <summary>
/// Test Settlement Militia Value changed for all clients
/// </summary>
public class SettlementMiltiaTest
{
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Used to test that the client recieves Miltia value changed.
    /// </summary>
    [Fact]
    public void ServerSettlementMilitiaChanged_Publishes_AllClients()
    {
        var settlement = TestEnvironment.Server.CreateRegisteredObject<Settlement>("settlement1");
        foreach (var client in TestEnvironment.Clients)
        {
            client.CreateRegisteredObject<Settlement>("settlement1");
        }

        var triggerMessage = new SettlementChangedMilitia(settlement, 50f);


        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeSettlementMilitia>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSettlementMilitia>());
        }

    }

}
