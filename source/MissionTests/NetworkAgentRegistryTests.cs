using Missions.Services.Network;
using System;
using LiteNetLib;
using System.Collections.Generic;
using System.Runtime.Serialization;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace MissionTests
{
    public class NetworkAgentRegistryTests
    {
        NetworkAgentRegistry networkAgentRegistry = new NetworkAgentRegistry();

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

        [Fact]
        public void FullTestRemote()
        {
            Agent localAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            localAgent.Age = 11;
            Guid localGuid = Guid.NewGuid();
            networkAgentRegistry.RegisterControlledAgent(localGuid, localAgent);

            Agent remoteAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            remoteAgent.Age = 10;
            Guid remoteGuid = Guid.NewGuid();
            NetPeer netPeer = (NetPeer)FormatterServices.GetUninitializedObject(typeof(NetPeer));
            networkAgentRegistry.RegisterNetworkControlledAgent(netPeer, remoteGuid, remoteAgent);

            networkAgentRegistry.TryGetAgent(remoteGuid, out Agent resolvedAgent);

            Assert.Equal(remoteAgent, resolvedAgent);
            Assert.Equal(remoteAgent.Age, resolvedAgent.Age);

            Assert.True(networkAgentRegistry.TryGetExternalController(remoteAgent, out var resolvedPeer));
            Assert.Equal(netPeer, resolvedPeer);

            Assert.True(networkAgentRegistry.TryGetExternalController(remoteGuid, out var resolvedPeerWithId));
            Assert.Equal(netPeer, resolvedPeerWithId);
        }
    }
}
