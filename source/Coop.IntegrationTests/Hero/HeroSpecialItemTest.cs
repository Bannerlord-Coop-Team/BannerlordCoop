using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.IntegrationTests.Hero;

/// <summary>
/// For the SpecialItem added test
/// </summary>
public class HeroSpecialItemTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Use for server controlled functionality
    /// </summary>
    [Fact]
    public void ServerSpecialItemCollection_PublishesHeroItemObjectProperty_AllClients()
    {
        // Arrange
        var triggerMessage = new HeroSpecialItemChanged("HERO1", "ITEMOBJ");

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a single message to it's game interface
        Assert.Equal(1, server.InternalMessages.GetMessageCount<HeroSpecialItemChanged>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<HeroAddSpecialItem>());
        }
    }
}
