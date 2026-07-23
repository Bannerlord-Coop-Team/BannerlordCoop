using Common.Util;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Template.Messages;
using TaleWorlds.CampaignSystem.Party;

namespace Coop.IntegrationTests.Template;

public class TestTemplate
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Use for server controlled functionality
    /// </summary>
    [Fact(Skip = "Example Test")]
    public void ServerReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients()
    {
        // Arrange
        var party1Id = "Party1";
        var party1 = ObjectHelper.SkipConstructor<MobileParty>();
        party1.StringId = party1Id;
        party1._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party1, party1Id);

        var triggerMessage = new TemplateEventMessage(party1, 1.0f);

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<TemplateNetworkMessage>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<TemplateNetworkMessage>());
        }
    }


    /// <summary>
    /// Use for client controlled functionality
    /// </summary>
    [Fact(Skip = "Example Test")]
    public void ClientReceivesTemplateEventMessage_PublishesTemplateCommandMessage_AllClients()
    {
        // Arrange
        var party1Id = "Party1";
        var party1 = ObjectHelper.SkipConstructor<MobileParty>();
        party1.StringId = party1Id;
        party1._attachedParties = new TaleWorlds.Library.MBList<MobileParty>();
        TestEnvironment.RegisterObjectInNetwork(party1, party1Id);

        var triggerMessage = new TemplateEventMessage(party1, 1.0f);

        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;

        // Act
        client1.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<TemplateNetworkMessage>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<TemplateNetworkMessage>());
        }
    }
}
