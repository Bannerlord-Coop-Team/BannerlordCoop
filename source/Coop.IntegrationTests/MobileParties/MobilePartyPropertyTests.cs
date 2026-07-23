using Coop.IntegrationTests.Environment;

namespace Coop.IntegrationTests.MobileParties
{
    public class MobilePartyPropertyTests
    {
        // Creates a test environment with 1 server and 2 clients by default
        private TestEnvironment TestEnvironment { get; } = new();

        ////Every property change uses the same messages thus one test will show same result as all others
        //[Fact]
        //public void ServerReceivedArmyChanged()
        //{
        //    var triggerMessage = new MobilePartyPropertyChanged(PropertyType.Army, "differentId", "testId");

        //    var server = TestEnvironment.Server;

        //    server.SimulateMessage(this, triggerMessage);

        //    Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkMobilePartyPropertyChanged>());

        //    foreach (EnvironmentInstance client in TestEnvironment.Clients)
        //    {
        //        Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeMobilePartyProperty>());
        //    }
        //}
    }
}
