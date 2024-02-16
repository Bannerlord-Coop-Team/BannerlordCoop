using Coop.Core.Server.Services.Fiefs.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Fiefs.Messages;

namespace Coop.IntegrationTests.Fiefs
{
    public class FiefFoodStockTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client receives FiefFoodStockChanged messages.
        /// </summary>
        [Fact]
        public void ServerFiefFoodStockChanged_Publishes_AllClients()
        {
            // Arrange
            string fiefId = "Settlement1";
            float foodStock = (float)100;
            var triggerMessage = new FiefFoodStockChanged(fiefId, foodStock);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // Assert
            // Verify the server sends a single message to its game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeFiefFoodStock>());

            //Verify if the server is sending the same foodStock and fiefId value
            Assert.Equal(foodStock, server.NetworkSentMessages.GetMessages<NetworkChangeFiefFoodStock>().First().FoodStockQuantity);
            Assert.Equal(fiefId, server.NetworkSentMessages.GetMessages<NetworkChangeFiefFoodStock>().First().FiefId);


            // Verify all clients receive a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeFiefFoodStock>());
            }

            // Verify all clients receive the same foodStock value and fiefId value
            foreach(EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(foodStock, client.InternalMessages.GetMessages<ChangeFiefFoodStock>().First().FoodStockQuantity);
                Assert.Equal(fiefId, client.InternalMessages.GetMessages<ChangeFiefFoodStock>().First().FiefId);
            }


        }
    }
}
