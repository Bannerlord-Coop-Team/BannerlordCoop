using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Utils;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.IntegrationTests.MobileParties;

public class AiBehaviorTest
{
    public TestEnvironment TestEnvironment { get; }

    public AiBehaviorTest()
    {
        TestEnvironment = new TestEnvironment();
    }

    [Fact]
    public void ControlledPartyAiBehaviorUpdated_Publishes_AllClients()
    {
        var message = Utils.Object.CreateUninitialized<ControlledPartyAiBehaviorUpdated>();

        var client1 = TestEnvironment.Clients.First();

        client1.SendMessageInternal(this, message);

        foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdatePartyAiBehavior>());
        }
    }
}
