using Common.Messaging;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Utils;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.IntegrationTests.MobileParties;

public class AiBehaviorTest
{
    internal TestEnvironment TestEnvironment { get; }

    public AiBehaviorTest()
    {
        TestEnvironment = new TestEnvironment();
    }

    /// <summary>
    /// Verify sending ControlledPartyAiBehaviorUpdated on one client
    /// Triggers UpdatePartyAiBehavior on all other clients
    /// </summary>
    [Fact]
    public void ControlledPartyAiBehaviorUpdated_Publishes_AllClients()
    {
        // Arrange
        var message = ObjectHelper.SkipConstructor<ControlledPartyAiBehaviorUpdated>();

        var client1 = TestEnvironment.Clients.First();

        // Act
        client1.SendMessageInternal(this, message);

        // Assert
        foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdatePartyAiBehavior>());
        }
    }
}
