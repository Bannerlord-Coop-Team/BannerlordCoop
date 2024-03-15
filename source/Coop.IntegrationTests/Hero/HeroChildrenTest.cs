using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Template.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameInterface.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;

namespace Coop.IntegrationTests.Hero;

/// <summary>
/// Used to test Child sync works
/// </summary>
public class HeroChildrenTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Use for server controlled functionality
    /// </summary>
    [Fact]
    public void ServerHeroChildrenProperty_PublishesHeroChildrenProperty_AllClients()
    {
        // Arrange
        var triggerMessage = new NewChildrenAdded("HERO1", "CHILD1");

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<NewChildrenAdded>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<AddNewChildren>());
        }
    }

}
