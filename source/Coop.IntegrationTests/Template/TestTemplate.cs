using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Template.Messages;

namespace Coop.IntegrationTests.Template;

public class TestTemplate
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
        var triggerMessage = new TemplateEventMessage();

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<TemplateCommandMessage>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<TemplateCommandMessage>());
        }
    }


    /// <summary>
    /// Use for client controlled functionality
    /// </summary>
    [Fact]
    public void ClientReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients()
    {
        // Arrange
        var triggerMessage = new TemplateEventMessage();

        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;

        // Act
        client1.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<TemplateCommandMessage>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<TemplateCommandMessage>());
        }
    }
}
