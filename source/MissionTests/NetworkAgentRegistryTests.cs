using GameInterface.Missions;
using System;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace MissionTests
{
    public class NetworkAgentRegistryTests
    {
        private readonly NetworkAgentRegistry registry = new NetworkAgentRegistry();

        private static Agent NewAgent(int age)
        {
            var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            agent.Age = age;
            return agent;
        }

        [Fact]
        public void RegisterAgent_ResolvesByAgentAndId()
        {
            var partyId = Guid.NewGuid();
            var agentId = Guid.NewGuid();
            var agent = NewAgent(10);

            Assert.True(registry.TryRegisterAgent(partyId, agentId, agent));

            Assert.True(registry.TryGetAgentInfo(agent, out var byAgent));
            Assert.True(registry.TryGetAgentInfo(agentId, out var byId));

            Assert.Equal(agent, byAgent.Agent);
            Assert.Equal(agentId, byAgent.AgentId);
            Assert.Equal(partyId, byAgent.PartyId);
            Assert.Equal(byAgent.AgentId, byId.AgentId);
            Assert.Equal(byAgent.PartyId, byId.PartyId);
        }

        [Fact]
        public void RegisterAgent_DuplicateFails()
        {
            var partyId = Guid.NewGuid();
            var agentId = Guid.NewGuid();
            var agent = NewAgent(10);

            Assert.True(registry.TryRegisterAgent(partyId, agentId, agent));
            Assert.False(registry.TryRegisterAgent(partyId, agentId, agent));          // same agent + id
            Assert.False(registry.TryRegisterAgent(partyId, Guid.NewGuid(), agent));   // same agent, new id
        }

        [Fact]
        public void RegisterAgent_RejectsEmptyIds()
        {
            var agent = NewAgent(10);

            Assert.False(registry.TryRegisterAgent(Guid.Empty, Guid.NewGuid(), agent));
            Assert.False(registry.TryRegisterAgent(Guid.NewGuid(), Guid.Empty, agent));
            Assert.False(registry.TryRegisterAgent(Guid.NewGuid(), Guid.NewGuid(), null));
        }

        [Fact]
        public void RemoveAgent_ClearsBothIndexes()
        {
            var agentId = Guid.NewGuid();
            var agent = NewAgent(10);
            registry.TryRegisterAgent(Guid.NewGuid(), agentId, agent);

            Assert.True(registry.RemoveAgent(agentId));
            Assert.False(registry.TryGetAgentInfo(agent, out _));
            Assert.False(registry.TryGetAgentInfo(agentId, out _));
            Assert.False(registry.RemoveAgent(agentId));
        }
    }
}
