using Coop.Core.Client.Services.Heroes.Messages;
using Coop.IntegrationTests.Environment;
using Coop.IntegrationTests.Environment.Instance;
using GameInterface.Services.Heroes.Messages;

namespace Coop.IntegrationTests.Heroes
{
    public class HeroFieldTests
    {
        // Creates a test environment with 1 server and 2 clients by default
        internal TestEnvironment TestEnvironment { get; } = new TestEnvironment();

        [Fact]
        public void ServerRecievesLastTimeStampChanged()
        {
            var triggerMessage = new LastTimeStampChanged(2, "testId");

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
            var triggerMessage = new CharacterObjectChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkCharacterObjectChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeCharacterObject>());
            }
        }
        [Fact]
        public void ServerRecievesFirstNameChanged()
        {
            var triggerMessage = new FirstNameChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkFirstNameChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeFirstName>());
            }
        }
        [Fact]
        public void ServerRecievesNameChanged()
        {
            var triggerMessage = new NameChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkNameChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeName>());
            }
        }
        [Fact]
        public void ServerRecievesHairTagsChanged()
        {
            var triggerMessage = new HairTagsChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkHairTagsChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeHairTags>());
            }
        }
        [Fact]
        public void ServerRecievesBeardTagsChanged()
        {
            var triggerMessage = new BeardTagsChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkBeardTagsChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeBeardTags>());
            }
        }
        [Fact]
        public void ServerRecievesTattooTagsChanged()
        {
            var triggerMessage = new TattooTagsChanged("testChar", "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkTattooTagsChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeTattooTags>());
            }
        }
        [Fact]
        public void ServerRecievesHeroStateChanged()
        {
            var triggerMessage = new HeroStateChanged(2, "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkHeroStateChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeHeroState>());
            }
        }
        [Fact]
        public void ServerRecievesHeroLevelChanged()
        {
            var triggerMessage = new HeroLevelChanged(2, "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkHeroLevelChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeHeroLevel>());
            }
        }
        [Fact]
        public void ServerRecievesSpcDaysInLocationChanged()
        {
            var triggerMessage = new SpcDaysInLocationChanged(2, "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkSpcDaysInLocationChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangeSpcDaysInLocation>());
            }
        }
        [Fact]
        public void ServerRecievesDefaultAgeChanged()
        {
            var triggerMessage = new DefaultAgeChanged(2f, "testId");

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
            var triggerMessage = new BirthDayChanged(2L, "testId");

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
            var triggerMessage = new PowerChanged(2f, "testId");

            var server = TestEnvironment.Server;

            server.SimulateMessage(this, triggerMessage);

            Assert.Equal(1, server.NetworkSentMessages.GetMessageCount<NetworkPowerChanged>());

            foreach (EnvironmentInstance client in TestEnvironment.Clients)
            {
                Assert.Equal(1, client.InternalMessages.GetMessageCount<ChangePower>());
            }
        }
    }
}
