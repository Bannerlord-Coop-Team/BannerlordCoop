using Coop.Core.Server.Services.Villages.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Template.Messages;
using GameInterface.Services.Villages.Messages;

namespace Coop.IntegrationTests.Villages;

public class VillageStateTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Use for server controlled functionality
    /// </summary>
    [Fact]
    public void ServerReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients()
    {
        // Arrange
        string settlementId = "Settlement1";
        int state = 0;
        var triggerMessage = new VillageStateChanged(settlementId, state);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeVillageState>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeVillageState>());
        }
    }

}
