using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Packets;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Utils;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using TaleWorlds.CampaignSystem;

namespace Coop.IntegrationTests.MobileParties;

public class TimeControlTest
{
    internal TestEnvironment TestEnvironment { get; }

    public TimeControlTest()
    {
        // Creates a test environment with 1 server and 2 clients by default
        TestEnvironment = new TestEnvironment();
    }

    /// <summary>
    /// Verify sending ControlledPartyBehaviorUpdated on one client
    /// Triggers UpdatePartyBehavior on all other clients
    /// </summary>
    [Fact]
    public void ControlledPartyBehaviorUpdated_Publishes_AllClients()
    {
        // Arrange
        var message = new TimeSpeedChanged(CampaignTimeControlMode.StoppablePlay);
        var client1 = TestEnvironment.Clients.First();

        // Act
        client1.SendMessageInternal(this, message);

        // Assert
        foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<SetTimeControlMode>());
        }
    }
}
