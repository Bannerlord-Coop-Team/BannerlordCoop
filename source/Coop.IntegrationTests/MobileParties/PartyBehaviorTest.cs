using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Utils;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.IntegrationTests.MobileParties;

public class PartyBehaviorTest
{
    internal TestEnvironment TestEnvironment { get; }

    public PartyBehaviorTest()
    {
        // Creates a test environment with 1 server and 2 clients by default
        TestEnvironment = new TestEnvironment();
    }

    /// <summary>
    /// Verify sending ControlledPartyBehaviorUpdated on the server
    /// UpdatePartyBehavior triggers only on the server
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_Server()
    {
        // Arrange
        var message = ObjectHelper.SkipConstructor<ControlledPartyBehaviorUpdated>();

        var client1 = TestEnvironment.Clients.First();
        var server = TestEnvironment.Server;

        // Act
        client1.ReceiveMessage(this, message);

        // Assert
        Assert.Equal(1, server.InternalMessages.GetMessageCount<UpdatePartyBehavior>());

        // Only publishes on the server
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Equal(0, client.InternalMessages.GetMessageCount<UpdatePartyBehavior>());
        }
    }

    /// <summary>
    /// Verify when the server internally receives PartyBehaviorUpdated
    /// UpdatePartyBehavior triggers on all other clients
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_AllClients()
    {
        // Arrange
        var data = new PartyBehaviorUpdateData("Test_Party", default, default, default, default, default);

        var message = new PartyBehaviorUpdated(ref data);

        var server = TestEnvironment.Server;

        // Act
        server.ReceiveMessage(this, message);

        /// wait for polling task to complete <see cref="RequestMobilePartyBehaviorPacketHandler.Poll"/>
        Thread.Sleep(1000);

        // Assert
        foreach (var client in TestEnvironment.Clients)
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdatePartyBehavior>());
        }
    }
}
