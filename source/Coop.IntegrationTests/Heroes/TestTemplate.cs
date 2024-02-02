using Common.Util;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Heroes.Messages;

namespace Coop.IntegrationTests.Heroes;

public class HeroCreatedTest
{
    // Creates a test environment with 1 server and 2 clients by default
    internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

    /// <summary>
    /// Use for server controlled functionality
    /// </summary>
    [Fact]
    public void HeroCreated_Publishes_CreateHero_AllClients()
    {
        // Arrange
        var triggerMessage = ObjectHelper.SkipConstructor<HeroCreated>();

        var server = TestEnvironment.Server;

        // Act
        server.SimulateMessage(this, triggerMessage);

        // Assert
        // Verify the server sends a command to all clients 
        Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkCreateHero>());

        // Verify the all clients send a single message to their game interfaces
        foreach (EnvironmentInstance client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<CreateHero>());
        }
    }
}
