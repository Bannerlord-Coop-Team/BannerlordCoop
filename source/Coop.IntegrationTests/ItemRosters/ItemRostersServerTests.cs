using Coop.IntegrationTests.Environment.Instance;
using Coop.IntegrationTests.Environment;
using GameInterface.Services.ItemRosters.Messages;

using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
namespace Coop.IntegrationTests.ItemRosters
{
    public class ItemRostersServerTests
    {
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        /// <summary>
        /// Checks if ItemRosterUpdated triggered on server, triggers UpdateItemRoster on all clients.
        /// </summary>
        [Fact]
        public void ServerReceivesItemRosterUpdated_PublishesUpdateItemRoster_AllClients()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
            }

            var item = TestEnvironment.Server.CreateRegisteredObject<ItemObject>("item1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemObject>("item1");
            }

            var triggerMessage = new ItemRosterUpdated(itemRoster, item, null, 1);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.InternalMessages.GetMessageCount<ItemRosterUpdated>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<UpdateItemRoster>());
            }
        }

        /// <summary>
        /// Checks if ItemRosterCleared triggered on server, triggers ClearItemRoster on all clients.
        /// </summary>
        [Fact]
        public void ServerReceivesItemRosterCleared_PublishesClearItemRoster_AllClients()
        {
            var itemRoster = TestEnvironment.Server.CreateRegisteredObject<ItemRoster>("itemRoster1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<ItemRoster>("itemRoster1");
            }

            var triggerMessage = new ItemRosterCleared(itemRoster);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.InternalMessages.GetMessageCount<ItemRosterCleared>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ClearItemRoster>());
            }
        }
    }
}
