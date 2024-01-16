using Coop.IntegrationTests.Environment;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Messages;
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
    /// Verify sending TimeSpeedChanged on one client
    /// Triggers SetTimeControlMode on all other clients
    /// </summary>
    [Fact]
    public void SetTimeControlMode_Publishes_AllClients()
    {
        // Arrange
        var message = new AttemptedTimeSpeedChanged(TimeControlEnum.Play_1x);
        var client1 = TestEnvironment.Clients.First();

        // Act
        client1.SimulateMessage(this, message);

        // Assert
        foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
        {
            Assert.Equal(1, client.InternalMessages.GetMessageCount<SetTimeControlMode>());
        }
    }
}
