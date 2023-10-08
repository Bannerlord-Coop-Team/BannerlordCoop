using Coop.IntegrationTests.Environment;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.IntegrationTests.MobileParties
{
    public class MobilePartyHeroesTest
    {
        internal TestEnvironment TestEnvironment { get; }

        public MobilePartyHeroesTest()
        {
            // Creates a test environment with 1 server and 2 clients by default
            TestEnvironment = new TestEnvironment();
        }

        /// <summary>
        /// Verify sending AddHeroToParty on one client
        /// Triggers HeroAddedToParty on all other clients
        /// </summary>
        [Fact]
        public void AddHeroToParty_Publishes_AllClients()
        {
            // Arrange
            var partyId = "Test Party";
            var heroId = "Hero Id";

            var message = new AddHeroToParty(heroId, partyId, true);

            var client1 = TestEnvironment.Clients.First();

            // Act
            client1.ReceiveMessage(this, message);

            // Assert
            foreach (var client in TestEnvironment.Clients.Where(c => c != client1))
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<HeroAddedToParty>());
            }
        }
    }
}