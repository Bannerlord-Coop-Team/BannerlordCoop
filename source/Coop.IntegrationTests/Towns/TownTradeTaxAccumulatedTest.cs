using Common.Network;
using Common.Network.Coalescing;
using Coop.Core.Server.Services.Towns.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Towns.Messages;

using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Towns
{
    public class TownTradeTaxAccumulatedTest
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Used to Test that client recieves TownTradeTaxAccumulatedChanged messages.
        /// </summary>
        [Fact]
        public void ServerTownTradeTaxAccumulatedChanged_Publishes_AllClients()
        {
            // Arrange
            var town = TestEnvironment.Server.CreateRegisteredObject<Town>("town1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Town>("town1");
            }

            var triggerMessage = new TownTradeTaxAccumulatedChanged(town, 450);

            var server = TestEnvironment.Server;

            // Act
            server.SimulateMessage(this, triggerMessage);

            // The update is coalesced, so it only reaches clients once the server flushes for the tick.
            FlushCoalescer(server);

            // Assert
            // Verify the server sends a single message to it's game interface
            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownTradeTaxAccumulated>());

            // Verify the all clients send a single message to their game interfaces
            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTownTradeTaxAccumulated>());
            }
        }

        /// <summary>
        /// Repeated trade-tax changes to the same town within a tick collapse into one latest-wins send.
        /// </summary>
        [Fact]
        public void ServerCoalescesTownTradeTaxAccumulated_SendsLatestOnly()
        {
            // Arrange
            var town = TestEnvironment.Server.CreateRegisteredObject<Town>("town1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Town>("town1");
            }

            var server = TestEnvironment.Server;

            // Act: two trade-tax changes for the same town, the latest value being 777.
            server.SimulateMessage(this, new TownTradeTaxAccumulatedChanged(town, 450));
            server.SimulateMessage(this, new TownTradeTaxAccumulatedChanged(town, 777));

            // Nothing goes out until the tick flush.
            Assert.Equal(0, server.NetworkSentMessages.GetMessageCount<NetworkChangeTownTradeTaxAccumulated>());

            FlushCoalescer(server);

            // Assert: the two changes collapse into one send carrying the latest accumulated value.
            var sent = Assert.Single(server.NetworkSentMessages.GetMessages<NetworkChangeTownTradeTaxAccumulated>());
            Assert.Equal(777, sent.TradeTaxAccumulated);

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                var update = Assert.Single(client.InternalMessages.GetMessages<ChangeTownTradeTaxAccumulated>());
                Assert.Equal(777, update.TradeTaxAccumulated);
            }
        }

        // Drains the server's per-tick coalescer the way CoopServer.Update does, inside the server's
        // static scope so the merged send routes to clients.
        private static void FlushCoalescer(EnvironmentInstance server)
        {
            server.Call(() => server.Resolve<ISendCoalescer>().Flush(server.Resolve<INetwork>()));
        }
    }
}
