using Common.Messaging;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace MissionTests
{
    public class NetworkAgentRegistryTests
    {
        NetworkAgentRegistry networkAgentRegistry = new NetworkAgentRegistry(new MessageBroker());


        [Fact]
        public void FullTestLocal()
        {
            Agent newAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            newAgent.Age = 10;
            Guid guid = Guid.NewGuid();

            networkAgentRegistry.RegisterControlledAgent(guid, newAgent);

            networkAgentRegistry.TryGetAgent(guid, out Agent testAgent);
            networkAgentRegistry.TryGetAgentId(testAgent, out Guid testId);

            Assert.True(networkAgentRegistry.IsControlled(newAgent));
            Assert.True(networkAgentRegistry.IsControlled(guid));
            Assert.True(networkAgentRegistry.IsAgentRegistered(newAgent));
            Assert.True(networkAgentRegistry.IsAgentRegistered(guid));
            Assert.Equal(guid, testId);
            Assert.Equal(newAgent.Age, testAgent.Age);
            Assert.Equal(newAgent, testAgent);
        }
    }
}
