using Common.Messaging;
using LiteNetLib;
using Missions.Services.Network;
using Moq;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace IntroductionServerTests.Network
{
    /// <summary>
    /// Test class for <see cref="INetworkAgentRegistry"/>.
    /// </summary>
    public class NetworkAgentRegistryTests
    {
        INetworkAgentRegistry _agentRegistry;

        public NetworkAgentRegistryTests()
        {
            var messageBroker = new Mock<IMessageBroker>();

            _agentRegistry = new NetworkAgentRegistry(messageBroker.Object);
        }

        [Fact]
        public void RegisterControlledAgent_AddToEmptyDictionary()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();

            // Act
            var result = _agentRegistry.RegisterControlledAgent(guid, agent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.Empty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.ControlledAgents.ContainsKey(guid));
        }

        [Fact]
        public void RegisterControlledAgent_TryAddAlreadyExistingAgent()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();

            // Act
            _agentRegistry.RegisterControlledAgent(guid, agent);

            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.Empty(_agentRegistry.PlayerAgents);

            var result = _agentRegistry.RegisterControlledAgent(guid, agent);

            // Assert
            Assert.False(result);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.ControlledAgents.ContainsKey(guid));
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.Empty(_agentRegistry.PlayerAgents);
        }

        [Fact]
        public void RegisterNetworkControlledAgent_AddToEmptyDictionary()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            // Act
            var result = _agentRegistry.RegisterNetworkControlledAgent(peer, guid, agent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.Empty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
        }

        [Fact]
        public void RegisterNetworkControlledAgent_AddAgentToAlreadyExistingPeer()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            var otherGuid = Guid.NewGuid();
            var otherAgent = CreateAgent();

            // Act
            _agentRegistry.RegisterNetworkControlledAgent(peer, guid, agent);
            var result = _agentRegistry.RegisterNetworkControlledAgent(peer, otherGuid, otherAgent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.Empty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.AgentToId.ContainsKey(otherAgent));
        }

        [Fact]
        public void RegisterNetworkControlledAgent_TryAddAlreadyExistingAgent()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            // Act
            _agentRegistry.RegisterNetworkControlledAgent(peer, guid, agent);
            var result = _agentRegistry.RegisterNetworkControlledAgent(peer, guid, agent);

            // Assert
            Assert.False(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.Empty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
        }

        [Fact]
        public void RegisterPlayerAgent_AddToEmptyDictionary()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();

            // Act
            var result = _agentRegistry.RegisterPlayerAgent(guid, agent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.ControlledAgents.ContainsKey(guid));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));
        }

        [Fact]
        public void RegisterPlayerAgent_TryAddAlreadyExistingAgent()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();

            // Act
            _agentRegistry.RegisterPlayerAgent(guid, agent);

            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.ControlledAgents.ContainsKey(guid));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));

            var result = _agentRegistry.RegisterPlayerAgent(guid, agent);

            // Assert
            Assert.False(result);

            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.ControlledAgents.ContainsKey(guid));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));
        }

        [Fact]
        public void RegisterNetworkPlayerAgent_AddToEmptyDictionary()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            // Act
            var result = _agentRegistry.RegisterNetworkPlayerAgent(peer, guid, agent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));
        }

        [Fact]
        public void RegisterNetworkPlayerAgent_AddAgentToAlreadyExistingPeer()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            var otherGuid = Guid.NewGuid();
            var otherAgent = CreateAgent();

            // Act
            _agentRegistry.RegisterNetworkPlayerAgent(peer, guid, agent);
            var result = _agentRegistry.RegisterNetworkPlayerAgent(peer, otherGuid, otherAgent);

            // Assert
            Assert.True(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.AgentToId.ContainsKey(otherAgent));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(otherGuid));
        }

        [Fact]
        public void RegisterNetworkPlayerAgent_TryAddAlreadyExistingAgent()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            // Act
            _agentRegistry.RegisterNetworkPlayerAgent(peer, guid, agent);
            var result = _agentRegistry.RegisterNetworkPlayerAgent(peer, guid, agent);

            // Assert
            Assert.False(result);
            Assert.NotEmpty(_agentRegistry.OtherAgents);
            Assert.NotEmpty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.PlayerAgents);
            Assert.True(_agentRegistry.AgentToId.ContainsKey(agent));
            Assert.True(_agentRegistry.PlayerAgents.ContainsKey(guid));
        }

        [Fact]
        public void RemoveControlledAgent_NoneExists()
        {
            // Arrange
            var guid = Guid.NewGuid();

            // Act
            var result = _agentRegistry.RemoveControlledAgent(guid);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemoveControlledAgent_OneExists()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();

            _agentRegistry.RegisterControlledAgent(guid, agent);

            // Act
            var result = _agentRegistry.RemoveControlledAgent(guid);

            // Assert
            Assert.True(result);
            Assert.Empty(_agentRegistry.AgentToId);
            Assert.NotEmpty(_agentRegistry.ControlledAgents);
        }

        [Fact]
        public void RemovePeer_NoneExists()
        {
            // Arrange
            var peer = CreateNetPeer();

            // Act
            var result = _agentRegistry.RemovePeer(peer);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void RemovePeer_ControlledAgentExists()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            _agentRegistry.RegisterNetworkControlledAgent(peer, guid, agent);

            // Act
            var result = _agentRegistry.RemovePeer(peer);

            // Assert
            Assert.True(result);
            Assert.Empty(_agentRegistry.AgentToId);
            Assert.Empty(_agentRegistry.OtherAgents);
            Assert.Empty(_agentRegistry.PlayerAgents);
        }

        [Fact]
        public void RemovePeer_PlayerAgentExists()
        {
            // Arrange
            var guid = Guid.NewGuid();
            var agent = CreateAgent();
            var peer = CreateNetPeer();

            _agentRegistry.RegisterNetworkPlayerAgent(peer, guid, agent);

            // Act
            var result = _agentRegistry.RemovePeer(peer);

            // Assert
            Assert.True(result);
            Assert.Empty(_agentRegistry.AgentToId);
            Assert.Empty(_agentRegistry.OtherAgents);
            Assert.Empty(_agentRegistry.PlayerAgents);
        }

        private Agent CreateAgent() => FormatterServices.GetSafeUninitializedObject(typeof(Agent)) as Agent;
        private NetPeer CreateNetPeer() => FormatterServices.GetSafeUninitializedObject(typeof(NetPeer)) as NetPeer;
    }
}
