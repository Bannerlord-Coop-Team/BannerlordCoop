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
    }
}
