using Coop.Core.Client.Services.MobileParties.Messages.Data;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.MobileParties.Messages.Data;

namespace Coop.IntegrationTests.MobileParties;

public class PartyArmyChangedTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    [Fact]
    public void ServerReceivesPartyArmyChanged_PublishesChangePartyArmy_AllClients()
    {
        // Arrange
        var triggerMessage = new PartyArmyChanged(null);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangePartyArmy>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangePartyArmy>());
        }
    }
}
