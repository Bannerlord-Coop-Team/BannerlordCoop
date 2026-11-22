using Coop.Core.Client.Services.Heroes.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Heroes.Messages;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
namespace Coop.IntegrationTests.Heroes
{
    public class HeroFieldTests
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        [Fact]
        public void ServerRecievesLastTimeStampChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new LastTimeStampChanged(5, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkLastTimeStampChanged>());

            foreach(EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeLastTimeStamp>());
            }
        }
        [Fact]
        public void ServerRecievesCharacterObjectChanged()
        {
            var characterObject = TestEnvironment.Server.CreateRegisteredObject<CharacterObject>("characterObject1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<CharacterObject>("characterObject1");
            }

            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new CharacterObjectChanged(characterObject, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkCharacterObjectChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeCharacterObject>());
            }
        }
        [Fact]
        public void ServerRecievesHeroStateChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new HeroStateChanged(1, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkHeroStateChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeHeroState>());
            }
        }
        [Fact]
        public void ServerRecievesDefaultAgeChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new DefaultAgeChanged(25f, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkDefaultAgeChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeDefaultAge>());
            }
        }
        [Fact]
        public void ServerRecievesBirthDayChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new BirthDayChanged(100L, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkBirthDayChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeBirthDay>());
            }
        }
        [Fact]
        public void ServerRecievesPowerChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new PowerChanged(10f, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkPowerChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangePower>());
            }
        }
        [Fact]
        public void ServerRecievesHomeSettlementChanged()
        {
            var settlement = TestEnvironment.Server.CreateRegisteredObject<Settlement>("settlement1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Settlement>("settlement1");
            }

            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new HomeSettlementChanged(settlement, hero);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkHomeSettlementChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeHomeSettlement>());
            }
        }
        [Fact]
        public void ServerRecievesPregnantChanged()
        {
            var hero = TestEnvironment.Server.CreateRegisteredObject<Hero>("hero1");
            foreach (var client in TestEnvironment.Clients)
            {
                client.CreateRegisteredObject<Hero>("hero1");
            }

            var triggerMessage = new PregnantChanged(hero, true);

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkPregnantChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangePregnant>());
            }
        }
    }
}
