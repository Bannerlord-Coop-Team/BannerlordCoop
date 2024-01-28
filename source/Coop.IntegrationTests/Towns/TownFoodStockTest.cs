using Coop.Core.Server.Services.Clans.Messages;
using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;

namespace Coop.IntegrationTests.Towns
{
    public class TownFoodStockTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives TownFoodStockChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownFoodStockChanged_Publishes_AllClients()
        {
            // Arrange
            string townId = "Settlement1";
            double foodStock = (double)100;
            var triggerMessage = new TownFoodStockChanged(townId, foodStock);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownFoodStock>());

            //Verify if the server is sending the same foodStock and townId value
            Assert.Equal(foodStock, server.NetworkSentMessages.GetMessages<NetworkChangeTownFoodStock>().First().FoodStockQuantity);
            Assert.Equal(townId, server.NetworkSentMessages.GetMessages<NetworkChangeTownFoodStock>().First().TownId);


            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownFoodStock>());
            }

            // Verify all clients receive the same foodStock value and townId value
            foreach(EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(foodStock, client.InternalMessages.GetMessages<ChangeTownFoodStock>().First().FoodStockQuantity);
                Assert.Equal(townId, client.InternalMessages.GetMessages<ChangeTownFoodStock>().First().TownId);
            }


        }
    }
}
